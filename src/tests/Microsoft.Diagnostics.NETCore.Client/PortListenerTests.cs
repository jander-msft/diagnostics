// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics.NETCore.Client
{
    public class PortListenerTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public PortListenerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        /// <summary>
        /// Tests that listener throws appropriate exceptions when not started.
        /// </summary>
        [Fact]
        public async Task PortListenerNoStartTest()
        {
            await using var listener = CreatePortListener(out string transportName);
            // Intentionally did not start listener

            TimeSpan CancellationTimeout = TimeSpan.FromSeconds(1);
            using CancellationTokenSource cancellation = new CancellationTokenSource(CancellationTimeout);

            // All API surface (except for Start) should throw InvalidOperationException
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => listener.AcceptAsync(cancellation.Token));

            Assert.Throws<InvalidOperationException>(
                () => listener.Connect(Guid.Empty, CancellationTimeout));

            Assert.Throws<InvalidOperationException>(
                () => listener.RemoveConnection(Guid.Empty));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => listener.WaitForConnectionAsync(Guid.Empty, cancellation.Token));
        }

        /// <summary>
        /// Tests that listener throws appropriate exceptions when disposed.
        /// </summary>
        [Fact]
        public async Task PortListenerDisposeTest()
        {
            var listener = CreatePortListener(out string transportName);
            listener.Start();

            using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            Task acceptTask = listener.AcceptAsync(cancellation.Token);

            // Validate listener surface throws after disposal
            await listener.DisposeAsync();

            // Pending tasks should throw ObjectDisposedException
            await Assert.ThrowsAnyAsync<ObjectDisposedException>(() => acceptTask);
            Assert.True(acceptTask.IsFaulted);

            // Calls after dispose should throw ObjectDisposedException
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => listener.AcceptAsync(cancellation.Token));

            Assert.Throws<ObjectDisposedException>(
                () => listener.RemoveConnection(Guid.Empty));
        }

        /// <summary>
        /// Tests that <see cref="DiagnosticPortListener.AcceptAsync(CancellationToken)"/> does not complete
        /// when no connections are available and that cancellation will move the returned task to the cancelled state.
        /// </summary>
        [Fact]
        public async Task PortListenerAcceptAsyncYieldsTest()
        {
            await using var listener = CreatePortListener(out string transportName);
            listener.Start();

            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            _outputHelper.WriteLine("Waiting for connection from listener.");
            Task acceptTask = listener.AcceptAsync(cancellationSource.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acceptTask);
            Assert.True(acceptTask.IsCanceled);
        }

        /// <summary>
        /// Tests that invoking listener methods with non-existing runtime identifier appropriately fail.
        /// </summary>
        [Fact]
        public async Task PortListenerNonExistingRuntimeIdentifierTest()
        {
            await using var listener = CreatePortListener(out string transportName);
            listener.Start();

            Guid nonExistingRuntimeId = Guid.NewGuid();

            using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            _outputHelper.WriteLine($"Testing {nameof(DiagnosticPortListener.WaitForConnectionAsync)}");
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => listener.WaitForConnectionAsync(nonExistingRuntimeId, cancellation.Token));

            _outputHelper.WriteLine($"Testing {nameof(DiagnosticPortListener.Connect)}");
            Assert.Throws<TimeoutException>(
                () => listener.Connect(nonExistingRuntimeId, TimeSpan.FromSeconds(1)));

            _outputHelper.WriteLine($"Testing {nameof(DiagnosticPortListener.RemoveConnection)}");
            Assert.False(listener.RemoveConnection(nonExistingRuntimeId), "Removal of nonexisting connection should fail.");
        }

        /// <summary>
        /// Tests that a single client can connect to the listener, diagnostics can occur,
        /// and multiple use of a single DiagnosticsClient is allowed.
        /// </summary>
        /// <remarks>
        /// The multiple use of a single client is important in the reverse scenario
        /// because of how the endpoint is updated with new stream information each
        /// time the target process reconnects to the listener.
        /// </remarks>
        [Fact(Skip = "Test fails in latest darc updates. See https://github.com/dotnet/diagnostics/issues/1482")]
        public async Task PortListenerSingleTargetMultipleUseClientTest()
        {
            await using var listener = CreatePortListener(out string transportName);
            listener.Start();

            TestRunner runner = null;
            IpcEndpointInfo info;
            try
            {
                // Start client pointing to listener
                runner = StartTracee(transportName);

                info = await AcceptAsync(listener);

                await VerifyEndpointInfo(runner, info);

                // There should not be any new endpoint infos
                await VerifyNoNewEndpointInfos(listener);

                ResumeRuntime(info);

                await VerifySingleSession(info);
            }
            finally
            {
                _outputHelper.WriteLine("Stopping tracee.");
                runner?.Stop();
            }

            // Wait some time for the process to exit
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Process exited so the endpoint should not have a valid transport anymore.
            await VerifyWaitForConnection(info, expectValid: false);

            Assert.True(listener.RemoveConnection(info.RuntimeInstanceCookie), "Expected to be able to remove connection from listener.");

            // There should not be any more endpoint infos
            await VerifyNoNewEndpointInfos(listener);
        }

        /// <summary>
        /// Tests that a DiagnosticsClient is not viable after target exists.
        /// </summary>
        [Fact(Skip = "Test fails in latest darc updates. See https://github.com/dotnet/diagnostics/issues/1482")]
        public async Task PortListenerSingleTargetExitsClientInviableTest()
        {
            await using var listener = CreatePortListener(out string transportName);
            listener.Start();

            TestRunner runner = null;
            IpcEndpointInfo info;
            try
            {
                // Start client pointing to diagnostics listener
                runner = StartTracee(transportName);

                // Get client connection
                info = await AcceptAsync(listener);

                await VerifyEndpointInfo(runner, info);

                // There should not be any new endpoint infos
                await VerifyNoNewEndpointInfos(listener);

                ResumeRuntime(info);

                await VerifyWaitForConnection(info);
            }
            finally
            {
                _outputHelper.WriteLine("Stopping tracee.");
                runner?.Stop();
            }

            // Wait some time for the process to exit
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Process exited so the endpoint should not have a valid transport anymore.
            await VerifyWaitForConnection(info, expectValid: false);

            Assert.True(listener.RemoveConnection(info.RuntimeInstanceCookie), "Expected to be able to remove connection from listener.");

            // There should not be any more endpoint infos
            await VerifyNoNewEndpointInfos(listener);
        }

        private DiagnosticPortListener CreatePortListener(out string transportName)
        {
            transportName = PortListenerHelper.CreateServerTransportName();
            _outputHelper.WriteLine("Starting listener at '" + transportName + "'.");
            return new DiagnosticPortListener(transportName);
        }

        private async Task<IpcEndpointInfo> AcceptAsync(DiagnosticPortListener listener)
        {
            using (var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                return await listener.AcceptAsync(cancellationSource.Token);
            }
        }

        private TestRunner StartTracee(string transportName)
        {
            _outputHelper.WriteLine("Starting tracee.");
            return PortListenerHelper.StartTracee(_outputHelper, transportName);
        }

        private static EventPipeProvider CreateProvider(string name)
        {
            return new EventPipeProvider(name, EventLevel.Verbose, (long)EventKeywords.All);
        }

        private async Task VerifyWaitForConnection(IpcEndpointInfo info, bool expectValid = true)
        {
            using var connectionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            if (expectValid)
            {
                await info.Endpoint.WaitForConnectionAsync(connectionCancellation.Token);
            }
            else
            {
                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => info.Endpoint.WaitForConnectionAsync(connectionCancellation.Token));
            }
        }

        /// <summary>
        /// Checks that the accepter does not provide a new endpoint info.
        /// </summary>
        private async Task VerifyNoNewEndpointInfos(DiagnosticPortListener listener)
        {
            _outputHelper.WriteLine("Verifying there are no more connections.");

            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            Task acceptTask = listener.AcceptAsync(cancellationSource.Token);
            await Assert.ThrowsAsync<TaskCanceledException>(() => acceptTask);
            Assert.True(acceptTask.IsCanceled);

            _outputHelper.WriteLine("Verified there are no more connections.");
        }

        /// <summary>
        /// Verifies basic information on the endpoint info and that it matches the target process from the runner.
        /// </summary>
        private async Task VerifyEndpointInfo(TestRunner runner, IpcEndpointInfo info, bool expectValid = true)
        {
            _outputHelper.WriteLine($"Verifying connection information for process ID {runner.Pid}.");
            Assert.NotNull(runner);
            Assert.Equal(runner.Pid, info.ProcessId);
            Assert.NotEqual(Guid.Empty, info.RuntimeInstanceCookie);
            Assert.NotNull(info.Endpoint);

            await VerifyWaitForConnection(info, expectValid);

            _outputHelper.WriteLine($"Connection: {info.ToTestString()}");
        }

        private void ResumeRuntime(IpcEndpointInfo info)
        {
            var client = new DiagnosticsClient(info.Endpoint);

            _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Resuming runtime instance.");
            try
            {
                client.ResumeRuntime();
                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Resumed successfully.");
            }
            catch (ServerErrorException ex)
            {
                // Runtime likely does not understand the ResumeRuntime command.
                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies that a client can handle multiple operations simultaneously.
        /// </summary>
        private async Task VerifySingleSession(IpcEndpointInfo info)
        {
            await VerifyWaitForConnection(info);

            var client = new DiagnosticsClient(info.Endpoint);

            _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Creating session #1.");
            var providers = new List<EventPipeProvider>();
            providers.Add(new EventPipeProvider(
                "System.Runtime",
                EventLevel.Informational,
                0,
                new Dictionary<string, string>() {
                    { "EventCounterIntervalSec", "1" }
                }));
            using var session = client.StartEventPipeSession(providers);

            _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Verifying session produces events.");
            await VerifyEventStreamProvidesEventsAsync(info, session, 1);

            _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session verification complete.");
        }

        /// <summary>
        /// Verifies that an event stream does provide events.
        /// </summary>
        private Task VerifyEventStreamProvidesEventsAsync(IpcEndpointInfo info, EventPipeSession session, int sessionNumber)
        {
            Assert.NotNull(session);
            Assert.NotNull(session.EventStream);

            return Task.Run(async () =>
            {
                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Creating event source.");

                // This blocks for a while due to this bug: https://github.com/microsoft/perfview/issues/1172
                using var eventSource = new EventPipeEventSource(session.EventStream);

                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Setup event handlers.");

                // Create task completion source that is completed when any events are provided; cancel it if cancellation is requested
                var receivedEventsSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                using var _ = cancellation.Token.Register(() =>
                {
                    if (receivedEventsSource.TrySetCanceled())
                    {
                        _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Cancelled event processing.");
                    }
                });

                // Create continuation task that stops the session (which immediately stops event processing).
                Task stoppedProcessingTask = receivedEventsSource.Task
                    .ContinueWith(_ =>
                    {
                        _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Stopping session.");
                        session.Stop();
                    });

                // Signal task source when an event is received.
                Action<TraceEvent> allEventsHandler = _ =>
                {
                    if (receivedEventsSource.TrySetResult(null))
                    {
                        _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Received an event and set result on completion source.");
                    }
                };

                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Start processing events.");
                eventSource.Dynamic.All += allEventsHandler;
                eventSource.Process();
                eventSource.Dynamic.All -= allEventsHandler;
                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Stopped processing events.");

                // Wait on the task source to verify if it ran to completion or was cancelled.
                await receivedEventsSource.Task;

                _outputHelper.WriteLine($"{info.RuntimeInstanceCookie}: Session #{sessionNumber} - Waiting for session to stop.");
                await stoppedProcessingTask;
            });
        }
    }
}
