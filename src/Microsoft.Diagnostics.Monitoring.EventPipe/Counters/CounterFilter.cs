// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal sealed class CounterFilter
    {
        private Dictionary<string, List<string>> _enabledCounters;
        private Dictionary<string, int> _intervalsMSec;

        public static CounterFilter AllCounters { get; } = new CounterFilter();

        public CounterFilter()
        {
            _enabledCounters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _intervalsMSec = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        // Called when we want to enable all counters under a provider name.
        public void AddFilter(string providerName, int intervalMSec)
        {
            AddFilter(providerName, intervalMSec, Array.Empty<string>());
        }

        public void AddFilter(string providerName, int intervalMSec, string[] counters)
        {
            _enabledCounters[providerName] = new List<string>(counters ?? Array.Empty<string>());
            _intervalsMSec[providerName] = intervalMSec;
        }

        public IEnumerable<string> GetProviders() => _enabledCounters.Keys;

        public bool IsIncluded(string providerName, IDictionary<string, object> payload)
        {
            return TryGetCounterName(payload, out string counterName) &&
                TryGetSeries(payload, out int seriesIntervalMSec) &&
                IsIncluded(providerName, seriesIntervalMSec, counterName);
        }

        public bool IsIncluded(string providerName, int intervalMSec, string counterName)
        {
            if (_enabledCounters.Count == 0)
            {
                return true;
            }
            if (_enabledCounters.TryGetValue(providerName, out List<string> enabledCounters) &&
                _intervalsMSec.TryGetValue(providerName, out int providerIntervalMSec))
            {
                return enabledCounters.Count == 0 || (enabledCounters.Contains(counterName, StringComparer.OrdinalIgnoreCase) && providerIntervalMSec == intervalMSec);
            }
            return false;
        }

        public static bool TryGetCounterName(IDictionary<string, object> payload, out string counterName)
        {
            return payload.TryGetValue("CounterName", out counterName);
        }

        public static bool TryGetSeries(IDictionary<string, object> payload, out int intervalMSec)
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
