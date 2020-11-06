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
        private Dictionary<string, int> _intervals;

        public static CounterFilter AllCounters { get; } = new CounterFilter();

        public CounterFilter()
        {
            //Provider names are not case sensitive, but counter names are.
            _enabledCounters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _intervals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        // Called when we want to enable all counters under a provider name.
        public void AddFilter(string providerName, int intervalMSec)
        {
            AddFilter(providerName, intervalMSec, Array.Empty<string>());
        }

        public void AddFilter(string providerName, int intervalMSec, string[] counters)
        {
            _enabledCounters[providerName] = new List<string>(counters ?? Array.Empty<string>());
            _intervals[providerName] = intervalMSec;
        }

        public IEnumerable<string> GetProviders() => _enabledCounters.Keys;

        public bool IsIncluded(string providerName, int intervalMSec, string counterName)
        {
            if (_enabledCounters.Count == 0)
            {
                return true;
            }
            if (_enabledCounters.TryGetValue(providerName, out List<string> enabledCounters) &&
                _intervals.TryGetValue(providerName, out int providerIntervalMSec))
            {
                return (enabledCounters.Count == 0 || enabledCounters.Contains(counterName, StringComparer.Ordinal)) &&
                    providerIntervalMSec == intervalMSec;
            }
            return false;
        }
    }
}
