// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public static class DiagnosticSourceTraceEventExtensions
    {
        public static IDictionary<string, string> GetDiagnosticSourceTraceEventArguments(this TraceEvent traceEvent)
        {
            IDictionary<string, string> arguments = new Dictionary<string, string>();
            if (traceEvent.PayloadByName("Arguments") is IEnumerable<object> argumentsPayload)
            {
                foreach (IDictionary<string, object> keyvalue in argumentsPayload)
                {
                    if (keyvalue.TryGetValue("Key", out string key) &&
                        keyvalue.TryGetValue("Value", out string value))
                    {
                        arguments.Add(key, value);
                    }
                }
            }
            return arguments;
        }

        public static string GetDiagnosticSourceTraceEventName(this TraceEvent traceEvent)
        {
            return traceEvent.PayloadStringByName("EventName");
        }

        public static string GetDiagnosticSourceTraceEventSourceName(this TraceEvent traceEvent)
        {
            return traceEvent.PayloadStringByName("SourceName");
        }
    }
}
