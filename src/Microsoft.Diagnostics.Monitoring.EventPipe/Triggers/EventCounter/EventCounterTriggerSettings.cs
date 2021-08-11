// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    internal sealed class EventCounterTriggerSettings
    {
        public string ProviderName { get; set; }

        public string CounterName { get; set; }

        public double? GreaterThan { get; set; }

        public double? LessThan { get; set; }

        public TimeSpan SlidingWindowDuration { get; set; }

        public int CounterIntervalSeconds { get; set; }
    }
}
