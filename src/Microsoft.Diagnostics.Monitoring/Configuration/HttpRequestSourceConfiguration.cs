// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;

namespace Microsoft.Diagnostics.Monitoring
{
    public sealed class HttpRequestSourceConfiguration : MonitoringSourceConfiguration
    {
        private const string DiagnosticFilterString =
                "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Start@Activity1Start:-" +
                    "Request.Scheme" +
                    ";Request.Host" +
                    ";Request.PathBase" +
                    ";Request.QueryString" +
                    ";Request.Path" +
                    ";Request.Method" +
                    ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
                    ";ActivityParentId=*Activity.ParentId" +
                    ";ActivityId=*Activity.Id" +
                    ";ActivitySpanId=*Activity.SpanId" +
                    ";ActivityTraceId=*Activity.TraceId" +
                    ";ActivityParentSpanId=*Activity.ParentSpanId" +
                    ";ActivityIdFormat=*Activity.IdFormat" +
                "\r\n" +
                "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop@Activity1Stop:-" +
                    "Response.StatusCode" +
                    ";ActivityDuration=*Activity.Duration.Ticks" +
                    ";ActivityId=*Activity.Id" +
                "\r\n" +
                "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut@Event:-" +
                "\r\n" +
                "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start@Activity2Start:-" +
                    "Request.RequestUri" +
                    ";Request.Method" +
                    ";Request.RequestUri.Host" +
                    ";Request.RequestUri.Port" +
                    ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
                    ";ActivityId=*Activity.Id" +
                    ";ActivitySpanId=*Activity.SpanId" +
                    ";ActivityTraceId=*Activity.TraceId" +
                    ";ActivityParentSpanId=*Activity.ParentSpanId" +
                    ";ActivityIdFormat=*Activity.IdFormat" +
                    ";ActivityId=*Activity.Id" +
                    "\r\n" +
                "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Stop@Activity2Stop:-" +
                    ";ActivityDuration=*Activity.Duration.Ticks" +
                    ";ActivityId=*Activity.Id" +
                "\r\n";

        private class Keywords
        {
            /// <summary>
            /// Indicates diagnostics messages from DiagnosticSourceEventSource should be included.
            /// </summary>
            public const EventKeywords Messages = (EventKeywords)0x1;
            /// <summary>
            /// Indicates that all events from all diagnostic sources should be forwarded to the EventSource using the 'Event' event.
            /// </summary>
            public const EventKeywords Events = (EventKeywords)0x2;

            // Some ETW logic does not support passing arguments to the EventProvider.   To get around
            // this in common cases, we define some keywords that basically stand in for particular common argumnents
            // That way at least the common cases can be used by everyone (and it also compresses things).
            // We start these keywords at 0x1000.   See below for the values these keywords represent
            // Because we want all keywords on to still mean 'dump everything by default' we have another keyword
            // IgnoreShorcutKeywords which must be OFF in order for the shortcuts to work thus the all 1s keyword
            // still means what you expect.
            public const EventKeywords IgnoreShortCutKeywords = (EventKeywords)0x0800;
            public const EventKeywords AspNetCoreHosting = (EventKeywords)0x1000;
            public const EventKeywords EntityFrameworkCoreCommands = (EventKeywords)0x2000;
        };

        public override IList<EventPipeProvider> GetProviders()
        {
            var providers = new List<EventPipeProvider>()
            {
                // Diagnostic source events
                new EventPipeProvider(DiagnosticSourceEventSource,
                        keywords: (long)(Keywords.Messages | Keywords.Events),
                        eventLevel: EventLevel.Verbose,
                        arguments: new Dictionary<string,string>
                        {
                            { "FilterAndPayloadSpecs", DiagnosticFilterString }
                        })
            };

            return providers;
        }
    }
}
