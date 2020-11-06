// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal static class TraceEventExtensions
    {
        public static bool TryGetCounterPayload(this TraceEvent traceEvent, CounterFilter filter, out IDictionary<string, object> payload)
        {
            payload = null;

            if (!string.Equals("EventCounters", traceEvent.EventName, StringComparison.Ordinal))
            {
                return false;
            }

            IDictionary<string, object> firstPayloadField = traceEvent.PayloadValue(0) as IDictionary<string, object>;
            if (null == firstPayloadField)
            {
                return false;
            }

            IDictionary<string, object> counterPayload;
            if (!firstPayloadField.TryGetValue("Payload", out counterPayload))
            {
                return false;
            }

            // Make sure we are part of the requested series. If multiple clients request metrics, all of them get the metrics.
            if (!TryGetCounterName(counterPayload, out string counterName) ||
                !TryGetSeries(counterPayload, out int seriesIntervalMSec) ||
                !filter.IsIncluded(traceEvent.ProviderName, seriesIntervalMSec, counterName))
            {
                return false;
            }

            payload = counterPayload;
            return true;
        }

        private static bool TryGetCounterName(IDictionary<string, object> payload, out string counterName)
        {
            return payload.TryGetValue("Name", out counterName);
        }

        private static bool TryGetSeries(IDictionary<string, object> payload, out int intervalMSec)
        {
            if (payload.TryGetValue("Series", out string series))
            {
                intervalMSec = GetInterval(series);
                return true;
            }

            intervalMSec = 0;
            return false;
        }

        private static int GetInterval(string series)
        {
            const string comparison = "Interval=";
            int interval = 0;
            if (series.StartsWith(comparison, StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(series.Substring(comparison.Length), out interval);
            }
            return interval;
        }
    }
}
