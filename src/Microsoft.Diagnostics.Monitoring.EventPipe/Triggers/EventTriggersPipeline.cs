// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public class EventTriggersPipeline : EventSourcePipeline<EventTriggersPipelineSettings>
    {
        public EventTriggersPipeline(DiagnosticsClient client, EventTriggersPipelineSettings settings)
            : base(client, settings)
        {
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return Settings.Configuration;
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource source, Func<Task> stopSessionAsync, CancellationToken token)
        {
            var stateMap = Settings.States.ToDictionary(s => s.Name);
            var evaluators = new List<EventTriggerEvaluator>();

            Func<string, string, EventFilterResponse> eventFilter = (providerName, eventName) =>
            {
                if (evaluators.Any(e => e.CanAccept(providerName, eventName)))
                {
                    return EventFilterResponse.AcceptEvent;
                }
                else
                {
                    return EventFilterResponse.RejectEvent;
                }
            };

            IDictionary<string, int> intervalMap = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var provider in Settings.Configuration.GetProviders())
            {
                if (null != provider.Arguments &&
                    provider.Arguments.TryGetValue("EventCounterIntervalSec", out string intervalSecString) &&
                    int.TryParse(intervalSecString, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int intervalSec))
                {
                    intervalMap.Add(provider.Name, intervalSec * 1000);
                }
            }

            using AutoResetEvent continueEvent = new AutoResetEvent(false);

            EventTriggersPipelineState currentState = Settings.States[0];
            string nextStateName = null;
            do
            {
                foreach (var trigger in currentState.Triggers)
                {
                    switch (trigger.Condition)
                    {
                        case EventTriggersPipelineEventTriggerCondition eventCondition:
                            if (eventCondition.Accessor is EventTriggersPipelineEventTriggerConditionAggregateAccessor)
                            {
                                throw new NotSupportedException();
                            }
                            else if (eventCondition.Accessor is EventTriggersPipelineEventTriggerConditionPropertyAccessor propertyAccessor)
                            {
                                if (string.IsNullOrEmpty(eventCondition.CounterName))
                                {
                                    evaluators.Add(new ScalarEventTriggerEvaluator(trigger, eventCondition, propertyAccessor));
                                }
                                else if (intervalMap.TryGetValue(eventCondition.ProviderName, out int intervalMSec))
                                {
                                    evaluators.Add(new ScalarCounterTriggerEvaluator(trigger, eventCondition, propertyAccessor, intervalMSec));
                                }
                            }
                            break;
                        case EventTriggersPipelineTimerTriggerCondition timerCondition:
                            throw new NotSupportedException();
                        default:
                            throw new NotSupportedException();
                    }
                }

                TaskCompletionSource<EventTriggersPipelineStateTrigger> tcs = new TaskCompletionSource<EventTriggersPipelineStateTrigger>();
                Action<TraceEvent> eventHandler = (traceEvent) =>
                {
                    foreach (var evaluator in evaluators)
                    {
                        if (evaluator.AcceptAndEvaluate(traceEvent))
                        {
                            tcs.SetResult(evaluator.Trigger);
                            continueEvent.WaitOne();
                            break;
                        }
                    }
                };

                source.Dynamic.AddCallbackForProviderEvents(eventFilter, eventHandler);

                if (!string.IsNullOrEmpty(nextStateName))
                {
                    continueEvent.Set();
                }

                EventTriggersPipelineStateTrigger satisfiedTrigger = await tcs.Task;

                source.Dynamic.RemoveCallback(eventHandler);

                evaluators.Clear();

                nextStateName = satisfiedTrigger.TargetState;
            }
            while (stateMap.TryGetValue(nextStateName, out currentState));
        }

        private abstract class EventTriggerEvaluator
        {
            public EventTriggerEvaluator(EventTriggersPipelineStateTrigger trigger, string providerName, string eventName)
            {
                Trigger = trigger;
                ProviderName = providerName;
                EventName = eventName;
            }

            public bool CanAccept(string providerName, string eventName)
            {
                return string.Equals(providerName, ProviderName, StringComparison.Ordinal) &&
                    string.Equals(eventName, EventName, StringComparison.Ordinal);
            }

            public abstract bool AcceptAndEvaluate(TraceEvent traceEvent);

            public string ProviderName { get; }

            public string EventName { get; }

            public EventTriggersPipelineStateTrigger Trigger { get; }
        }

        private class ScalarEventTriggerEvaluator : EventTriggerEvaluator
        {
            private readonly string _operatorName;
            private readonly string _propertyName;
            private readonly string _value;

            private Func<object, bool> _condition;

            public ScalarEventTriggerEvaluator(
                EventTriggersPipelineStateTrigger trigger,
                EventTriggersPipelineEventTriggerCondition condition,
                EventTriggersPipelineEventTriggerConditionPropertyAccessor accessor)
                : base(trigger, condition.ProviderName, condition.EventName)
            {
                _propertyName = accessor.PropertyName;
                _operatorName = condition.Operator;
                _value = condition.Value;
            }

            public override bool AcceptAndEvaluate(TraceEvent traceEvent)
            {
                if (!TryGetPayloadValue(traceEvent, _propertyName, out object value))
                {
                    return false;
                }

                Func<object, bool> condition = GetCondition(value);
                if (null == condition)
                {
                    return false;
                }

                return condition(value);
            }

            private Func<object, bool> GetCondition(object propertyValue)
            {
                if (null != _condition)
                {
                    return _condition;
                }

                if (null != propertyValue)
                {
                    Type propertyType = propertyValue.GetType();

                    var valueParamExpr = Expression.Parameter(typeof(object), "o");
                    var castValueExpr = Expression.Convert(valueParamExpr, propertyType);

                    Type formatProviderType = Type.GetType("System.IFormatProvider");
                    var convertConstMethod = Type.GetType("System.Convert").GetMethod(
                        "To" + propertyType.Name,
                        BindingFlags.Public | BindingFlags.Static,
                        Type.DefaultBinder,
                        new[] { typeof(string), formatProviderType },
                        new[] { new ParameterModifier() });

                    var convertedValue = convertConstMethod.Invoke(null, new object[] { _value, CultureInfo.InvariantCulture });

                    var constExpr = Expression.Constant(convertedValue, propertyType);

                    ExpressionType binaryOperator;
                    switch (_operatorName)
                    {
                        case ">":
                            binaryOperator = ExpressionType.GreaterThan;
                            break;
                        case ">=":
                            binaryOperator = ExpressionType.GreaterThanOrEqual;
                            break;
                        case "<":
                            binaryOperator = ExpressionType.LessThan;
                            break;
                        case "<=":
                            binaryOperator = ExpressionType.LessThanOrEqual;
                            break;
                        case "==":
                            binaryOperator = ExpressionType.Equal;
                            break;
                        case "!=":
                            binaryOperator = ExpressionType.NotEqual;
                            break;
                        default:
                            return null;
                    }

                    var comparisonExpr = Expression.MakeBinary(binaryOperator, castValueExpr, constExpr);
                    var funcExpr = Expression.Lambda<Func<object, bool>>(comparisonExpr, valueParamExpr);

                    _condition = funcExpr.Compile();
                }

                return _condition;
            }

            protected virtual bool TryGetPayloadValue(TraceEvent traceEvent, string propertyName, out object value)
            {
                int payloadIndex = traceEvent.PayloadIndex(_propertyName);
                if (payloadIndex < 0)
                {
                    value = null;
                    return false;
                }

                value = traceEvent.PayloadValue(payloadIndex);
                return true;
            }
        }

        private class ScalarCounterTriggerEvaluator : ScalarEventTriggerEvaluator
        {
            private readonly CounterFilter _filter;

            public ScalarCounterTriggerEvaluator(
                EventTriggersPipelineStateTrigger trigger,
                EventTriggersPipelineEventTriggerCondition condition,
                EventTriggersPipelineEventTriggerConditionPropertyAccessor accessor,
                int intervalMSec)
                : base(trigger, condition, accessor)
            {
                _filter = new CounterFilter();
                _filter.AddFilter(condition.ProviderName, intervalMSec, new[] { condition.CounterName });
            }

            protected override bool TryGetPayloadValue(TraceEvent traceEvent, string propertyName, out object value)
            {
                value = null;

                if (!traceEvent.TryGetCounterPayload(_filter, out IDictionary<string, object> payload))
                {
                    return false;
                }

                return payload.TryGetValue(propertyName, out value);
            }
        }
    }
}
