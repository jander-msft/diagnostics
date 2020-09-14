// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Monitoring.EventPipe;

namespace DotnetCounters.UnitTests
{
    class TestHelpers
    {
        public static ICounterPayload GenerateCounterPayload(
            string provider,
            bool isIncrementingCounter,
            string counterName,
            double counterValue,
            int displayRateTimeScaleSeconds = 0,
            string displayName = "",
            string displayUnits = "")
        {
            if (isIncrementingCounter)
            {
                IDictionary<string, object> payloadFields = new Dictionary<string, object>()
                {
                    { "Name", counterName },
                    { "Increment", counterValue },
                    { "DisplayName", displayName },
                    { "DisplayRateTimeScale", displayRateTimeScaleSeconds == 0 ? "" : TimeSpan.FromSeconds(displayRateTimeScaleSeconds).ToString() },
                    { "DisplayUnits", displayUnits },
                };
                ICounterPayload payload = new IncrementingCounterPayload(provider, payloadFields, 1);
                return payload;
            }
            else
            {
                Dictionary<string, object> payloadFields = new Dictionary<string, object>()
                {
                    { "Name", counterName },
                    { "Mean", counterValue },
                    { "DisplayName", displayName },
                    { "DisplayUnits", displayUnits },
                };
                ICounterPayload payload = new CounterPayload(provider, payloadFields);
                return payload;
            }
        }
    }
}
