// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal static class TraceEventExtensions
    {
        public static bool TryGetCounterPayload(this TraceEvent traceEvent, out IDictionary<string, object> payload)
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

            return firstPayloadField.TryGetValue("Payload", out payload);
        }
    }
}
