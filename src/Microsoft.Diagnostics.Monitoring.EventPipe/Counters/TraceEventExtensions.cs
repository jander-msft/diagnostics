using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Counters
{
    internal static class TraceEventExtensions
    {
        public static bool TryGetCounterPayload(this TraceEvent traceEvent, out ICounterPayload payload)
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

            if (!firstPayloadField.TryGetValue("Payload", out IDictionary<string, object> counterPayload))
            {
                return false;
            }

            if (!counterPayload.TryGetValue("Series", out string series) ||
                !counterPayload.TryGetValue("Name", out string counterName) ||
                !counterPayload.TryGetValue("IntervalSec", out float intervalSec) ||
                !counterPayload.TryGetValue("DisplayName", out string displayName) ||
                !counterPayload.TryGetValue("DisplayUnits", out string displayUnits) ||
                !counterPayload.TryGetValue("CounterType", out string counterTypeString))
            {
                return false;
            }

            MetricType counterType;
            double value;
            double min = double.NaN;
            double max = double.NaN;
            double stddev = double.NaN;
            switch (counterTypeString)
            {
                case "Mean":
                    counterType = MetricType.Avg;
                    if (!counterPayload.TryGetValue("Mean", out value) ||
                        !counterPayload.TryGetValue("Min", out min) ||
                        !counterPayload.TryGetValue("Max", out max) ||
                        !counterPayload.TryGetValue("StdDev", out stddev))
                    {
                        return false;
                    }
                    break;
                case "Sum":
                    counterType = MetricType.Sum;
                    if (!counterPayload.TryGetValue("Increment", out value))
                    {
                        return false;
                    }
                    if (string.IsNullOrEmpty(displayUnits))
                    {
                        displayUnits = "count";
                    }
                    break;
                default:
                    return false;
            }

            payload = new Metric(
                traceEvent.TimeStamp,
                traceEvent.ProviderName,
                counterName,
                displayName,
                displayUnits,
                value,
                counterType,
                intervalSec
                );

            return true;
        }

        private static bool TryGetValue<T>(this IDictionary<string, object> dictionary, string key, out T value)
        {
            if (dictionary.TryGetValue(key, out object objValue) && objValue is T tValue)
            {
                value = tValue;
                return true;
            }

            value = default;
            return false;
        }
    }
}
