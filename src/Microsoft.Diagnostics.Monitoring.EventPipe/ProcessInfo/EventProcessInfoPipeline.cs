// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public class EventProcessInfoPipeline : EventSourcePipeline<EventProcessInfoPipelineSettings>
    {
        public delegate Task CommandLineCallback(string commandLine, CancellationToken token);

        private readonly CommandLineCallback _commandLineCallback;

        public EventProcessInfoPipeline(DiagnosticsClient client, EventProcessInfoPipelineSettings settings, CommandLineCallback commandLinkCallback)
            : base(client, settings)
        {
            _commandLineCallback = commandLinkCallback ?? throw new ArgumentNullException(nameof(commandLinkCallback));
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return new SampleProfilerConfiguration();
        }

        protected override Task OnEventSourceAvailable(EventPipeEventSource source, Func<Task> stopSessionAsync, CancellationToken token)
        {
            source.Dynamic.AddCallbackForProviderEvent(MonitoringSourceConfiguration.EventPipeProviderName, "ProcessInfo", traceEvent =>
            {
                _commandLineCallback((string)traceEvent.PayloadByName("CommandLine"), token);
            });

            source.Dynamic.All += traceEvent =>
            {
                stopSessionAsync();
            };

            return Task.CompletedTask;
        }
    }
}
