// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal class EventTriggersPipeline : EventSourcePipeline<EventTriggersPipelineSettings>
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

            CounterFilter filter = new CounterFilter();
            foreach (var provider in Settings.Configuration.GetProviders())
            {
                if (null != provider.Arguments &&
                    provider.Arguments.TryGetValue("EventCounterIntervalSec", out string intervalSecString) &&
                    int.TryParse(intervalSecString, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int intervalSec))
                {
                    filter.AddFilter(provider.Name, intervalSec * 1000);
                }
            }

            EventTriggersPipelineState currentState = Settings.States[0];
            string nextStateName = null;
            do
            {
                foreach (var trigger in currentState.Triggers)
                {
                    switch (trigger.Condition)
                    {
                        case EventTriggersPipelineEventTriggerCondition eventCondition:
                            evaluators.Add(EventTriggerEvaluator.FromTrigger(trigger, eventCondition, filter));
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
                            break;
                        }
                    }
                };

                source.Dynamic.AddCallbackForProviderEvents(eventFilter, eventHandler);

                EventTriggersPipelineStateTrigger satisfiedTrigger = await tcs.Task;

                source.Dynamic.RemoveCallback(eventHandler);

                evaluators.Clear();

                nextStateName = satisfiedTrigger.TargetState;
            }
            while (stateMap.TryGetValue(nextStateName, out currentState));
        }

        private abstract class EventTriggerEvaluator
        {
            public EventTriggerEvaluator(EventTriggersPipelineStateTrigger trigger, string providerName, string eventName, string counterName)
            {
                Trigger = trigger;
                ProviderName = providerName;
                EventName = eventName;
                CounterName = counterName;
            }

            public static EventTriggerEvaluator FromTrigger(EventTriggersPipelineStateTrigger trigger, EventTriggersPipelineEventTriggerCondition condition, CounterFilter filter)
            {
                Debug.Assert(trigger.Condition == condition);
                switch (condition.Accessor)
                {
                    case EventTriggersPipelineEventTriggerConditionAggregateAccessor:
                        throw new NotSupportedException();
                    case EventTriggersPipelineEventTriggerConditionPropertyAccessor propertyAccessor:
                        return new ScalarEventTriggerEvaluator(trigger, condition, propertyAccessor, filter);
                }
                throw new NotSupportedException();
            }

            public bool CanAccept(string providerName, string eventName)
            {
                return string.Equals(providerName, ProviderName, StringComparison.Ordinal) &&
                    string.Equals(eventName, EventName, StringComparison.Ordinal);
            }

            public abstract bool AcceptAndEvaluate(TraceEvent traceEvent);

            public string ProviderName { get; }

            public string EventName { get; }

            public string CounterName { get; }

            public EventTriggersPipelineStateTrigger Trigger { get; }
        }

        private class ScalarEventTriggerEvaluator : EventTriggerEvaluator
        {
            private readonly CounterFilter _filter;
            private readonly string _propertyName;

            public ScalarEventTriggerEvaluator(
                EventTriggersPipelineStateTrigger trigger,
                EventTriggersPipelineEventTriggerCondition condition,
                EventTriggersPipelineEventTriggerConditionPropertyAccessor accessor,
                CounterFilter filter)
                : base(trigger, condition.ProviderName, condition.EventName, condition.CounterName)
            {
                _filter = filter;
                _propertyName = accessor.PropertyName;
            }

            public override bool AcceptAndEvaluate(TraceEvent traceEvent)
            {
                object valueObj = null;
                if (traceEvent.TryGetCounterPayload(out IDictionary<string, object> payload))
                {
                    if (!_filter.IsIncluded(traceEvent.ProviderName, payload))
                    {
                        return false;
                    }

                    if (!payload.TryGetValue(_propertyName, out object value))
                    {
                        return false;
                    }

                    valueObj = value;
                }
                else
                {
                    int payloadIndex = traceEvent.PayloadIndex(_propertyName);
                    if (payloadIndex < 0)
                    {
                        return false;
                    }
                    valueObj = traceEvent.PayloadValue(payloadIndex);
                }

                return false;
            }
        }
    }
}
