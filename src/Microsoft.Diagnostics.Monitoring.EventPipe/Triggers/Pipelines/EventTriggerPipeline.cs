// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class EventTriggerPipeline<TEvent> : Pipeline
    {
        private readonly Action<TEvent> _callback;
        private readonly TaskCompletionSource<object> _completionSource =
            new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IEventTriggerEventSource<TEvent> _eventSource;
        private readonly List<int> _registrations = new List<int>();
        private readonly IEventTrigger<TEvent> _trigger;

        public EventTriggerPipeline(
            IEventTriggerEventSource<TEvent> eventSource,
            IEventTrigger<TEvent> trigger,
            IEnumerable<EventTriggerSubscriptionDescriptor> descriptors,
            Action<TEvent> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));

            foreach (EventTriggerSubscriptionDescriptor descriptor in descriptors)
            {
                _registrations.Add(_eventSource.AddCallback(
                    descriptor.ProviderName,
                    descriptor.EventName,
                    CheckEvent));
            }
        }

        protected override async Task OnRun(CancellationToken token)
        {
            using var _ = token.Register(() => _completionSource.TrySetCanceled(token));

            await _completionSource.Task.ConfigureAwait(false);
        }

        protected override Task OnStop(CancellationToken token)
        {
            _completionSource.TrySetResult(null);

            return base.OnStop(token);
        }

        protected override Task OnCleanup()
        {
            _completionSource.TrySetCanceled();

            foreach (int id in _registrations)
            {
                _eventSource.RemoveCallback(id);
            }

            return base.OnCleanup();
        }

        private void CheckEvent(TEvent @event)
        {
            if (!_completionSource.Task.IsCompleted)
            {
                if (_trigger.HasSatisfiedCondition(@event))
                {
                    _callback(@event);
                }
            }
        }
    }
}
