// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Runtime;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    internal sealed class EventCounterTriggerFactory :
        IEventTriggerFactory<TraceEvent, EventCounterTriggerSettings>
    {
        public IEventTrigger<TraceEvent> CreateTrigger(EventCounterTriggerSettings settings)
        {
            return new EventCounterTrigger(settings);
        }

        public IEnumerable<EventTriggerSubscriptionDescriptor> GetDescriptors(EventCounterTriggerSettings settings)
        {
            yield return new EventTriggerSubscriptionDescriptor() { EventName = "EventCounters", ProviderName = "System.Runtime" };
        }
    }
}
