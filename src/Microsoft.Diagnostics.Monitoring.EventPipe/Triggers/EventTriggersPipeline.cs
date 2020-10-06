// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            var evaluators = new List<EventConditionEvaluator>();

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

            EventTriggersPipelineState currentState = Settings.States[0];
            string nextStateName = null;
            do
            {
                foreach (var trigger in currentState.Triggers)
                {
                    evaluators.Add(EventConditionEvaluator.FromTrigger(trigger));
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

        private abstract class EventConditionEvaluator
        {
            public EventConditionEvaluator(EventTriggersPipelineStateTrigger trigger, string providerName, string eventName, string counterName)
            {
                Trigger = trigger;
                ProviderName = providerName;
                EventName = eventName;
                CounterName = counterName;
            }

            public static EventConditionEvaluator FromTrigger(EventTriggersPipelineStateTrigger trigger)
            {
                switch (trigger.Condition)
                {
                    case EventTriggersPipelineEventTriggerCondition eventTriggerCondition:
                        return new ScalarEventConditionEvaluator(
                            trigger,
                            eventTriggerCondition.ProviderName,
                            eventTriggerCondition.EventName,
                            eventTriggerCondition.CounterName);
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

        private class ScalarEventConditionEvaluator : EventConditionEvaluator
        {
            public ScalarEventConditionEvaluator(EventTriggersPipelineStateTrigger trigger, string providerName, string eventName, string counterName)
                : base(trigger, providerName, eventName, counterName)
            {
            }

            public override bool AcceptAndEvaluate(TraceEvent traceEvent)
            {
                return false;
            }
        }
    }
}
