// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class EventPipeTriggerPipeline<TOptions> :
        EventSourcePipeline<EventPipeTriggerPipelineSettings<TOptions>>
    {
        private readonly Action<TraceEvent> _callback;

        private EventTriggerPipeline<TraceEvent> _pipeline;
        private IEventTrigger<TraceEvent> _trigger;

        public EventPipeTriggerPipeline(DiagnosticsClient client, EventPipeTriggerPipelineSettings<TOptions> settings, Action<TraceEvent> callback) :
            base(client, settings)
        {
            if (null == Settings.TriggerFactory)
            {
                throw new ArgumentException(FormattableString.Invariant($"The {nameof(settings.TriggerFactory)} property on the settings must not be null."), nameof(settings));
            }

            _callback = callback;
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return Settings.Configuration;
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            _trigger = Settings.TriggerFactory.CreateTrigger(Settings.TriggerOptions);

            var descriptors = Settings.TriggerFactory.GetDescriptors(Settings.TriggerOptions);

            EventTriggerTraceEventSource eventTriggerEventSource = new(eventSource);

            _pipeline = new(eventTriggerEventSource, _trigger, descriptors, _callback);

            await _pipeline.RunAsync(token).ConfigureAwait(false);
        }

        protected override async Task OnStop(CancellationToken token)
        {
            if (null != _pipeline)
            {
                await _pipeline.StopAsync(token).ConfigureAwait(false);
            }
            await base.OnStop(token);
        }

        protected override async Task OnCleanup()
        {
            if (null != _pipeline)
            {
                await _pipeline.DisposeAsync().ConfigureAwait(false);
            }

            if (_trigger is IAsyncDisposable asyncDisposableTrigger)
            {
                await asyncDisposableTrigger.DisposeAsync().ConfigureAwait(false);
            }
            else if (_trigger is IDisposable disposableTrigger)
            {
                disposableTrigger.Dispose();
            }

            await base.OnCleanup();
        }
    }
}
