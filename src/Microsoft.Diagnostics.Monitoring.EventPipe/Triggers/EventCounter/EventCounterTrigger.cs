// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    internal sealed class EventCounterTrigger :
        ITraceEventTrigger
    {
        private readonly EventCounterTriggerImpl _impl;

        public EventCounterTrigger(EventCounterTriggerSettings settings)
        {
            _impl = new(settings);
        }

        public bool HasSatisfiedCondition(TraceEvent traceEvent)
        {
            if (traceEvent.TryGetCounterPayload(_impl.Filter, out ICounterPayload payload))
            {
                return _impl.HasSatisfiedCondition(payload);
            }
            return false;
        }

        public static MonitoringSourceConfiguration CreateConfiguration(EventCounterTriggerSettings settings)
        {
            return new MetricSourceConfiguration(settings.CounterIntervalSeconds, new string[] { settings.ProviderName });
        }
    }
}
