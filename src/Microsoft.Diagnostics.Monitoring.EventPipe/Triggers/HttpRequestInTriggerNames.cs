using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal static class HttpRequestTriggerNames
    {
        public const string OverThresholdRemainingRequests = nameof(OverThresholdRemainingRequests);

        public const string OverThreshold = nameof(OverThreshold);

        public const string RemainingRequests = nameof(RemainingRequests);
    }
}
