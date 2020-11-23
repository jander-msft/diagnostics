// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal abstract class EventEvaluator
    {
        private readonly CounterFilter _filter;
        private readonly string _eventName;
        private readonly string _providerName;

        protected EventEvaluator(string providerName, string eventName, string counterName, int counterIntervalMSec)
        {
            _providerName = providerName;
            _eventName = eventName;

            if (!string.IsNullOrEmpty(counterName))
            {
                _filter = new CounterFilter();
                _filter.AddFilter(providerName, counterIntervalMSec, new string[] { counterName });
            }
        }

        public bool Evaluate(TraceEvent value)
        {
            IDictionary<string, object> payload;
            if (null == _filter)
            {
                if (!string.Equals(_providerName, value.ProviderName, StringComparison.Ordinal) ||
                    !string.Equals(_eventName, value.EventName, StringComparison.Ordinal))
                {
                    return false;
                }

                payload = new Dictionary<string, object>(value.PayloadNames.Length);

                foreach (string payloadName in value.PayloadNames)
                {
                    payload.Add(payloadName, value.PayloadByName(payloadName));
                }

                return EvaluateCore(new TraceEventData(value, payload));
            }
            else if (value.TryGetCounterPayload(_filter, out payload))
            {
                return EvaluateCore(new TraceEventData(value, payload));
            }

            return false;
        }

        public abstract bool EvaluateCore(TraceEventData data);
    }
}
