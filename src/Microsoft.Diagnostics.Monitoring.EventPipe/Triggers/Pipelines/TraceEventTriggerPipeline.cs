// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class TraceEventTriggerPipeline : Pipeline
    {
        private readonly Action<TraceEvent> _callback;
        private readonly TaskCompletionSource<object> _completionSource;
        private readonly TraceEventSource _eventSource;
        private readonly ITraceEventTrigger _trigger;

        public TraceEventTriggerPipeline(TraceEventSource eventSource, ITraceEventTrigger trigger, Action<TraceEvent> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            _completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        }

        protected override async Task OnRun(CancellationToken token)
        {
            _eventSource.Dynamic.All += Dynamic_All;
            try
            {
                using var _ = token.Register(() => _completionSource.TrySetCanceled(token));

                await _completionSource.Task.ConfigureAwait(false);
            }
            finally
            {
                _eventSource.Dynamic.All -= Dynamic_All;
            }
        }

        protected override Task OnStop(CancellationToken token)
        {
            _completionSource.TrySetResult(null);

            return base.OnStop(token);
        }

        protected override Task OnCleanup()
        {
            _completionSource.TrySetCanceled();

            return base.OnCleanup();
        }

        private void Dynamic_All(TraceEvent obj)
        {
            if (!_completionSource.Task.IsCompleted)
            {
                if (_trigger.HasSatisfiedCondition(obj))
                {
                    _callback(obj);
                }
            }
        }
    }
}
