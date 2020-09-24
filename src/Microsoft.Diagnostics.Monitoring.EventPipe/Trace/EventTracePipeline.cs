// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Monitoring.Contracts;
using Microsoft.Diagnostics.NETCore.Client;
using DiagnosticsMonitor = Microsoft.Diagnostics.Monitoring.EventPipe.DiagnosticsEventPipeProcessor.DiagnosticsMonitor;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public class EventTracePipeline : Pipeline
    {
        public delegate Task StreamAvailableCallback(Stream stream, CancellationToken token);

        private readonly Lazy<DiagnosticsMonitor> _monitor;
        private readonly StreamAvailableCallback _onStreamAvailable;

        public DiagnosticsClient Client { get; }
        public EventTracePipelineSettings Settings { get; }

        public EventTracePipeline(DiagnosticsClient client, EventTracePipelineSettings settings, StreamAvailableCallback onStreamAvailable)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _onStreamAvailable = onStreamAvailable ?? throw new ArgumentNullException(nameof(onStreamAvailable));

            _monitor = new Lazy<DiagnosticsMonitor>(() => new DiagnosticsMonitor(Settings.Configuration));
        }

        protected override async Task OnRun(CancellationToken token)
        {
            Stream sessionStream = await _monitor.Value.ProcessEvents(Client, Settings.Duration, token);

            await _onStreamAvailable(sessionStream, token);
        }

        protected override Task OnStop(CancellationToken token)
        {
            if (_monitor.IsValueCreated)
            {
                _monitor.Value.StopProcessing();
            }
            return Task.CompletedTask;
        }

        protected override async ValueTask OnDispose()
        {
            if (_monitor.IsValueCreated)
            {
                await _monitor.Value.DisposeAsync();
            }
            await base.OnDispose();
        }
    }
}
