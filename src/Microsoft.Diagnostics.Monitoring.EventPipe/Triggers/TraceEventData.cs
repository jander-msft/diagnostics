// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class TraceEventData
    {
        private TraceEventData(TraceEvent traceEvent, IDictionary<string, object> payload)
        {
            Event = traceEvent;
            Payload = payload;
        }

        public static TraceEventData Create(TraceEvent traceEvent, CounterFilter filter)
        {
            if (!traceEvent.TryGetCounterPayload(filter, out IDictionary<string, object> payload))
            {
                payload = new Dictionary<string, object>(traceEvent.PayloadNames.Length);

                foreach (string payloadName in traceEvent.PayloadNames)
                {
                    payload.Add(payloadName, traceEvent.PayloadByName(payloadName));
                }
            }
            return new TraceEventData(traceEvent, payload);
        }

        public TraceEvent Event { get; }

        public IDictionary<string, object> Payload { get; }
    }
}
