// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal class EventTriggersPipelineSettings : EventSourcePipelineSettings
    {
        public MonitoringSourceConfiguration Configuration { get; set; }

        public EventTriggersPipelineState[] States { get; set; }
    }

    internal class EventTriggersPipelineState
    {
        public string Name { get; set; }

        public EventTriggersPipelineStateTrigger[] Triggers { get; set; }
    }

    internal class EventTriggersPipelineStateTrigger
    {
        public EventTriggersPipelineTriggerCondition Condition { get; set; }

        public EventTriggersPipelineExecutableAction[] Execute { get; set; }

        public string TargetState { get; set; }
    }

    internal abstract class EventTriggersPipelineTriggerCondition
    {
        public abstract string Type { get; }
    }

    internal class EventTriggersPipelineEventTriggerCondition :
        EventTriggersPipelineTriggerCondition
    {
        public override string Type => "event";

        public string ProviderName { get; set; }

        public string EventName { get; set; }

        public string CounterName { get; set; }

        public EventTriggersPipelineEventTriggerConditionPayloadAccessor Accessor { get; set; }

        public string Operator { get; set; }

        public string Value { get; set; }
    }

    internal abstract class EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public abstract string Type { get; }
    }

    internal class EventTriggersPipelineEventTriggerConditionPropertyAccessor :
        EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public override string Type => "property";

        public string PropertyName { get; set; }
    }

    internal class EventTriggersPipelineEventTriggerConditionAggregateAccessor :
        EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public override string Type => "aggregate";

        public string FunctionName { get; set; }

        public int EventCount { get; set; }

        public string PropertyName { get; set; }
    }

    internal class EventTriggersPipelineTimerTriggerCondition :
        EventTriggersPipelineTriggerCondition
    {
        public override string Type => "timer";

        public TimeSpan Expiration { get; set; }
    }

    internal class EventTriggersPipelineExecutableAction
    {
        public string Type { get; set; }
    }
}
