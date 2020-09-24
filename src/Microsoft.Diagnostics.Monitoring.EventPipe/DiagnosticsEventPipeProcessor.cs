// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal partial class DiagnosticsEventPipeProcessor : IAsyncDisposable
    {
        public delegate Task EventSourceAvailableCallback(EventPipeEventSource source, Func<Task> stopSessionAsync, CancellationToken token);

        private readonly MonitoringSourceConfiguration _configuration;

        private readonly object _lock = new object();

        private TaskCompletionSource<bool> _sessionStarted;
        private EventPipeEventSource _eventPipeSession;
        private bool _disposed;

        private readonly EventSourceAvailableCallback _onEventSourceAvailable;

        public DiagnosticsEventPipeProcessor(
            MonitoringSourceConfiguration configuration,
            EventSourceAvailableCallback onEventSourceAvailable)
        {
            _configuration = configuration;
            _onEventSourceAvailable = onEventSourceAvailable;

            _sessionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task Process(DiagnosticsClient client, TimeSpan duration, CancellationToken token)
        {
            //No need to guard against reentrancy here, since the calling pipeline does this already.
            await await Task.Factory.StartNew(async () =>
            {
                Task handleEventsTask = Task.CompletedTask;
                try
                {
                    using var _ = token.Register(() => _sessionStarted.TrySetCanceled());

                    await using var monitor = new DiagnosticsMonitor(_configuration);
                    Stream sessionStream = await monitor.ProcessEvents(client, duration, token);

                    EventPipeEventSource source = new EventPipeEventSource(sessionStream);

                    // Allows the event handling routines to stop processing before the duration expires.
                    Func<Task> stopFunc = () => Task.Run(() => { monitor.StopProcessing(); });

                    handleEventsTask = _onEventSourceAvailable(source, stopFunc, token);

                    lock(_lock)
                    {
                        _eventPipeSession = source;
                    }

                    if (!_sessionStarted.TrySetResult(true))
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    source.Process();

                    token.ThrowIfCancellationRequested();
                }
                catch (DiagnosticsClientException ex)
                {
                    throw new InvalidOperationException("Failed to start the event pipe session", ex);
                }
                finally
                {
                    EventPipeEventSource session = null;
                    lock (_lock)
                    {
                        session = _eventPipeSession;
                        _eventPipeSession = null;
                    }

                    session?.Dispose();
                }

                // Await the task returned by the event handling method AFTER the EventPipeEventSource is disposed.
                // The EventPipeEventSource will only raise the Completed event when it is disposed. So if this task
                // is waiting for the Completed event to be raised, it will never complete until after EventPipeEventSource
                // is diposed.
                await handleEventsTask;

            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async Task StopProcessing(CancellationToken token)
        {
            await _sessionStarted.Task;

            EventPipeEventSource session = null;
            lock (_lock)
            {
                session = _eventPipeSession;
            }
            if (session != null)
            {
                session.StopProcessing();
            }
        }

        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            _sessionStarted.TrySetCanceled();
            try
            {
                await _sessionStarted.Task;
            }
            catch
            {
            }

            _eventPipeSession?.Dispose();
        }
    }
}
