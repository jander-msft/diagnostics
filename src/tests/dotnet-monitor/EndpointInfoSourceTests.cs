﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Monitoring;
using Microsoft.Diagnostics.NETCore.Client;
using Xunit;
using Xunit.Abstractions;

namespace DotnetMonitor.UnitTests
{
    public class EndpointInfoSourceTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public EndpointInfoSourceTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        /// <summary>
        /// Tests that other <see cref="ListenModeEndpointInfoSource"> methods throw if
        /// <see cref="ListenModeEndpointInfoSource.Start"/> is not called.
        /// </summary>
        [Fact]
        public async Task ListenModeSourceNoStartTest()
        {
            await using var source = CreateListenModeSource(out string transportName);
            // Intentionally do not call Start

            TimeSpan CancellationTimeout = TimeSpan.FromSeconds(1);
            using CancellationTokenSource cancellation = new CancellationTokenSource(CancellationTimeout);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.GetEndpointInfoAsync(cancellation.Token));
        }

        /// <summary>
        /// Tests that the listen mode endpoint info source has not connections if no processes connect to it.
        /// </summary>
        [Fact]
        public async Task ListenModeSourceNoConnectionsTest()
        {
            await using var source = CreateListenModeSource(out _);
            source.Start();

            var endpointInfos = await GetEndpointInfoAsync(source);
            Assert.Empty(endpointInfos);
        }

        /// <summary>
        /// Tests that listen mode endpoint info source should throw ObjectDisposedException
        /// from API surface after being disposed.
        /// </summary>
        [Fact]
        public async Task ListenModeSourceThrowsWhenDisposedTest()
        {
            var source = CreateListenModeSource(out _);
            source.Start();

            await source.DisposeAsync();

            // Validate source surface throws after disposal
            Assert.Throws<ObjectDisposedException>(
                () => source.Start());

            Assert.Throws<ObjectDisposedException>(
                () => source.Start(1));

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => source.GetEndpointInfoAsync(CancellationToken.None));
        }

        /// <summary>
        /// Tests that listen mode endpoint info source should throw an exception from
        /// <see cref="ListenModeEndpointInfoSource.Start"/> and
        /// <see cref="ListenModeEndpointInfoSource.Start(int)"/> after listening was already started.
        /// </summary>
        [Fact]
        public async Task ListenModeSourceThrowsWhenMultipleStartTest()
        {
            await using var source = CreateListenModeSource(out _);
            source.Start();

            Assert.Throws<InvalidOperationException>(
                () => source.Start());

            Assert.Throws<InvalidOperationException>(
                () => source.Start(1));
        }

        /// <summary>
        /// Tests that the listen mode endpoint info source can properly enumerate endpoint infos when a single
        /// target connects to it and "disconnects" from it.
        /// </summary>
        [Fact(Skip = "Test fails in latest darc updates. See https://github.com/dotnet/diagnostics/issues/1482")]
        public async Task ListenModeSourceAddRemoveSingleConnectionTest()
        {
            await using var source = CreateListenModeSource(out string transportName);
            source.Start();

            var endpointInfos = await GetEndpointInfoAsync(source);
            Assert.Empty(endpointInfos);

            Task newEndpointInfoTask = source.WaitForNewEndpointInfoAsync(TimeSpan.FromSeconds(5));

            await using (var execution1 = StartTraceeProcess("LoggerRemoteTest", transportName))
            {
                await newEndpointInfoTask;

                execution1.Start();

                endpointInfos = await GetEndpointInfoAsync(source);

                var endpointInfo = Assert.Single(endpointInfos);
                VerifyConnection(execution1.TestRunner, endpointInfo);

                _outputHelper.WriteLine("Stopping tracee.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            endpointInfos = await GetEndpointInfoAsync(source);

            Assert.Empty(endpointInfos);
        }

        private TestListenModeEndpointInfoSource CreateListenModeSource(out string transportName)
        {
            transportName = PortListenerHelper.CreateServerTransportName();
            _outputHelper.WriteLine("Starting endpoint info source at '" + transportName + "'.");
            return new TestListenModeEndpointInfoSource(transportName, _outputHelper);
        }

        private RemoteTestExecution StartTraceeProcess(string loggerCategory, string transportName = null)
        {
            _outputHelper.WriteLine("Starting tracee.");
            string exePath = CommonHelper.GetTraceePath("EventPipeTracee", targetFramework: "net5.0");
            return RemoteTestExecution.StartProcess(exePath + " " + loggerCategory, _outputHelper, transportName);
        }

        private async Task<IEnumerable<IEndpointInfo>> GetEndpointInfoAsync(ListenModeEndpointInfoSource source)
        {
            _outputHelper.WriteLine("Getting endpoint infos.");
            using CancellationTokenSource cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await source.GetEndpointInfoAsync(cancellationSource.Token);
        }

        /// <summary>
        /// Verifies basic information on the connection and that it matches the target process from the runner.
        /// </summary>
        private static void VerifyConnection(TestRunner runner, IEndpointInfo endpointInfo)
        {
            Assert.NotNull(runner);
            Assert.NotNull(endpointInfo);
            Assert.Equal(runner.Pid, endpointInfo.ProcessId);
            Assert.NotEqual(Guid.Empty, endpointInfo.RuntimeInstanceCookie);
            Assert.NotNull(endpointInfo.Endpoint);
        }

        private sealed class TestListenModeEndpointInfoSource : ListenModeEndpointInfoSource
        {
            private readonly ITestOutputHelper _outputHelper;
            private readonly List<TaskCompletionSource<IpcEndpointInfo>> _addedEndpointInfoSources = new List<TaskCompletionSource<IpcEndpointInfo>>();

            public TestListenModeEndpointInfoSource(string transportPath, ITestOutputHelper outputHelper)
                : base(transportPath)
            {
                _outputHelper = outputHelper;
            }

            public async Task<IpcEndpointInfo> WaitForNewEndpointInfoAsync(TimeSpan timeout)
            {
                TaskCompletionSource<IpcEndpointInfo> addedEndpointInfoSource = new TaskCompletionSource<IpcEndpointInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var timeoutCancellation = new CancellationTokenSource();
                var token = timeoutCancellation.Token;
                using var _ = token.Register(() => addedEndpointInfoSource.TrySetCanceled(token));

                lock (_addedEndpointInfoSources)
                {
                    _addedEndpointInfoSources.Add(addedEndpointInfoSource);
                }

                _outputHelper.WriteLine("Waiting for new endpoint info.");
                timeoutCancellation.CancelAfter(timeout);
                IpcEndpointInfo endpointInfo = await addedEndpointInfoSource.Task;
                _outputHelper.WriteLine("Notified of new endpoint info.");

                return endpointInfo;
            }

            internal override void OnAddedEndpointInfo(IpcEndpointInfo info)
            {
                _outputHelper.WriteLine($"Added endpoint info to collection: {info.ToTestString()}");
                
                lock (_addedEndpointInfoSources)
                {
                    foreach (var source in _addedEndpointInfoSources)
                    {
                        source.TrySetResult(info);
                    }
                    _addedEndpointInfoSources.Clear();
                }
            }

            internal override void OnRemovedEndpointInfo(IpcEndpointInfo info)
            {
                _outputHelper.WriteLine($"Removed endpoint info from collection: {info.ToTestString()}");
            }
        }
    }
}
