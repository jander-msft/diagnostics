// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers;
using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.NETCore.Client.UnitTests;
using Microsoft.Diagnostics.Tracing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.UnitTests
{
    public class EventPipeTriggerPipelineUnitTests
    {
        private readonly ITestOutputHelper _output;

        public EventPipeTriggerPipelineUnitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestTraceEventTriggerPipeline()
        {
            const int Interval = 1; // 1 second

            await using (var testExecution = StartTraceeProcess("TriggerRemoteTest"))
            {
                //TestRunner should account for start delay to make sure that the diagnostic pipe is available.

                DiagnosticsClient client = new(testExecution.TestRunner.Pid);

                TaskCompletionSource<object> waitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

                await using EventPipeTriggerPipeline<int> pipeline = new(
                    client,
                    new EventPipeTriggerPipelineSettings<int>
                    {
                        Configuration = TestTrigger.CreateConfiguration(Interval),
                        TriggerFactory = new TestTriggerFactory(),
                        TriggerOptions = Interval,
                        Duration = Timeout.InfiniteTimeSpan
                    },
                    traceEvent =>
                    {
                        waitSource.SetResult(null);
                    });

                await PipelineTestUtilities.ExecutePipelineWithDebugee(
                    _output,
                    pipeline,
                    testExecution,
                    waitSource);
            }
        }

        private RemoteTestExecution StartTraceeProcess(string loggerCategory)
        {
            return RemoteTestExecution.StartProcess(CommonHelper.GetTraceePathWithArgs("EventPipeTracee") + " " + loggerCategory, _output);
        }

        private sealed class TestTriggerFactory : IEventTriggerFactory<TraceEvent, int>
        {
            public IEventTrigger<TraceEvent> CreateTrigger(int settings)
            {
                return new TestTrigger(settings);
            }

            public IEnumerable<EventTriggerSubscriptionDescriptor> GetDescriptors(int settings)
            {
                yield return new EventTriggerSubscriptionDescriptor() { EventName = "EventCounters", ProviderName = "System.Runtime" };
            }
        }

        public sealed class TestTrigger : IEventTrigger<TraceEvent>
        {
            private const string RuntimeEventProviderName = "System.Runtime";

            private readonly CounterFilter _filter;

            private int _eventCount = 0;

            public TestTrigger(int interval)
            {
                _filter = new CounterFilter(interval);
                _filter.AddFilter(RuntimeEventProviderName, new string[] { "cpu-usage" });
            }

            public bool HasSatisfiedCondition(TraceEvent traceEvent)
            {
                if (traceEvent.TryGetCounterPayload(_filter, out _))
                {
                    _eventCount++;
                }

                return _eventCount >= 3;
            }

            public static MonitoringSourceConfiguration CreateConfiguration(int settings)
            {
                return new MetricSourceConfiguration(settings, new string[] { RuntimeEventProviderName });
            }
        }
    }
}
