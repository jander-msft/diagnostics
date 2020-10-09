// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public class EventTriggersPipelineSettings : EventSourcePipelineSettings
    {
        public MonitoringSourceConfiguration Configuration { get; set; }

        public EventTriggersPipelineState[] States { get; set; }
    }

    public class EventTriggersPipelineState
    {
        public string Name { get; set; }

        public EventTriggersPipelineStateTrigger[] Triggers { get; set; }
    }

    public class EventTriggersPipelineStateTrigger
    {
        public EventTriggersPipelineTriggerCondition Condition { get; set; }

        public EventTriggersPipelineExecutableAction[] Execute { get; set; }

        public string TargetState { get; set; }
    }

    public abstract class EventTriggersPipelineTriggerCondition
    {
        public abstract string Type { get; }
    }

    public class EventTriggersPipelineEventTriggerCondition :
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

    public abstract class EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public abstract string Type { get; }
    }

    public class EventTriggersPipelineEventTriggerConditionPropertyAccessor :
        EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public override string Type => "property";

        public string PropertyName { get; set; }
    }

    public class EventTriggersPipelineEventTriggerConditionAggregateAccessor :
        EventTriggersPipelineEventTriggerConditionPayloadAccessor
    {
        public override string Type => "aggregate";

        public string FunctionName { get; set; }

        public int EventCount { get; set; }

        public string PropertyName { get; set; }
    }

    public class EventTriggersPipelineTimerTriggerCondition :
        EventTriggersPipelineTriggerCondition
    {
        public override string Type => "timer";

        public TimeSpan Expiration { get; set; }
    }

    public class EventTriggersPipelineExecutableAction
    {
        public string Type { get; set; }
    }
}
