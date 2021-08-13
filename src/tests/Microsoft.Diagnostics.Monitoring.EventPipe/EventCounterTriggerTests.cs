// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.UnitTests
{
    public class EventCounterTriggerTests
    {
        private readonly ITestOutputHelper _output;

        public EventCounterTriggerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that the trigger condition can be satisfied when detecting counter
        /// values higher than the specified threshold for a duration of time.
        /// </summary>
        [Fact]
        public void EventCounterTriggerGreaterThanTest()
        {
            // The counter value must be greater than 0.70 for at least 3 seconds.
            const double Threshold = 0.70; // 70%
            const int Interval = 1; // 1 second
            TimeSpan WindowDuration = TimeSpan.FromSeconds(3);

            CpuData[] data = new CpuData[]
            {
                new(0.65, false),
                new(0.67, false),
                new(0.71, false),
                new(0.73, false),
                new(0.72, true),
                new(0.71, true),
                new(0.70, false), // Value must be greater than threshold
                new(0.68, false),
                new(0.66, false),
                new(0.70, false),
                new(0.71, false),
                new(0.74, false),
                new(0.75, true),
                new(0.72, true),
                new(0.73, true),
                new(0.71, true),
                new(0.69, false),
                new(0.67, false)
            };

            EventCounterTriggerSettings settings = new()
            {
                ProviderName = EventCounterConstants.RuntimeProviderName,
                CounterName = EventCounterConstants.CpuUsageCounterName,
                GreaterThan = Threshold,
                CounterIntervalSeconds = Interval,
                SlidingWindowDuration = WindowDuration
            };

            SimulateDataVerifyTrigger(settings, data);
        }

        /// <summary>
        /// Test that the trigger condition can be satisfied when detecting counter
        /// values lower than the specified threshold for a duration of time.
        /// </summary>
        [Fact]
        public void EventCounterTriggerLessThanTest()
        {
            // The counter value must be less than 0.70 for at least 3 seconds.
            const double Threshold = 0.70; // 70%
            const int Interval = 1; // 1 second
            TimeSpan WindowDuration = TimeSpan.FromSeconds(3);

            CpuData[] data = new CpuData[]
            {
                new(0.65, false),
                new(0.67, false),
                new(0.68, true),
                new(0.69, true),
                new(0.70, false), // Value must be less than threshold
                new(0.71, false),
                new(0.68, false),
                new(0.66, false),
                new(0.67, true),
                new(0.65, true),
                new(0.64, true),
                new(0.71, false),
                new(0.73, false)
            };

            EventCounterTriggerSettings settings = new()
            {
                ProviderName = EventCounterConstants.RuntimeProviderName,
                CounterName = EventCounterConstants.CpuUsageCounterName,
                LessThan = Threshold,
                CounterIntervalSeconds = Interval,
                SlidingWindowDuration = WindowDuration
            };

            SimulateDataVerifyTrigger(settings, data);
        }

        /// <summary>
        /// Test that the trigger condition can be satisfied when detecting counter
        /// values that fall between two thresholds for a duration of time.
        /// </summary>
        [Fact]
        public void EventCounterTriggerRangeTest()
        {
            // The counter value must be between 0.25 and 0.35 for at least 8 seconds.
            const double LowerThreshold = 0.25; // 25%
            const double UpperThreshold = 0.35; // 35%
            const int Interval = 2; // 2 seconds
            TimeSpan WindowDuration = TimeSpan.FromSeconds(8);

            CpuData[] data = new CpuData[]
            {
                new(0.23, false),
                new(0.25, false),
                new(0.26, false),
                new(0.27, false),
                new(0.28, false),
                new(0.29, true),
                new(0.31, true),
                new(0.33, true),
                new(0.35, false),
                new(0.37, false),
                new(0.34, false),
                new(0.33, false),
                new(0.31, false),
                new(0.29, true),
                new(0.27, true),
                new(0.26, true),
                new(0.24, false)
            };

            EventCounterTriggerSettings settings = new()
            {
                ProviderName = EventCounterConstants.RuntimeProviderName,
                CounterName = EventCounterConstants.CpuUsageCounterName,
                GreaterThan = LowerThreshold,
                LessThan = UpperThreshold,
                CounterIntervalSeconds = Interval,
                SlidingWindowDuration = WindowDuration
            };

            SimulateDataVerifyTrigger(settings, data);
        }

        /// <summary>
        /// Test that the trigger condition will not be satisfied if successive
        /// counter events are missing from the stream (e.g. events are dropped due
        /// to event pipe buffer being filled).
        /// </summary>
        [Fact]
        public void EventCounterTriggerDropTest()
        {
            // The counter value must be greater than 0.50 for at least 10 seconds.
            const double Threshold = 0.50; // 50%
            const int Interval = 2; // 2 second
            TimeSpan WindowDuration = TimeSpan.FromSeconds(10);

            CpuData[] data = new CpuData[]
            {
                new(0.53, false),
                new(0.54, false),
                new(0.51, false),
                new(0.52, false),
                new(0.53, true),
                new(0.52, true, drop: true),
                new(0.51, false),
                new(0.54, false),
                new(0.58, false),
                new(0.53, false),
                new(0.51, true),
                new(0.54, true),
                new(0.54, true, drop: true),
                new(0.52, false),
                new(0.57, false),
                new(0.59, false),
                new(0.54, false),
                new(0.51, true),
                new(0.53, true),
                new(0.47, false)
            };

            EventCounterTriggerSettings settings = new()
            {
                ProviderName = EventCounterConstants.RuntimeProviderName,
                CounterName = EventCounterConstants.CpuUsageCounterName,
                GreaterThan = Threshold,
                CounterIntervalSeconds = Interval,
                SlidingWindowDuration = WindowDuration
            };

            SimulateDataVerifyTrigger(settings, data);
        }

        /// <summary>
        /// Run the specified sample CPU data through a simple simulation to test the capabilities
        /// of the event counter trigger. This uses a random number seed to generate random variations
        /// in timestamp and interval data.
        /// </summary>
        private void SimulateDataVerifyTrigger(EventCounterTriggerSettings settings, CpuData[] cpuData)
        {
            Random random = new Random();
            int seed = random.Next();
            _output.WriteLine("Simulation seed: {0}", seed);
            SimulateDataVerifyTrigger(settings, cpuData, seed);
        }

        /// <summary>
        /// Run the specified sample CPU data through a simple simulation to test the capabilities
        /// of the event counter trigger. This uses the specified seed value to seed the RNG that produces
        /// random variations in timestamp and interval data; allows for replayability of generated variations.
        /// </summary>
        private void SimulateDataVerifyTrigger(EventCounterTriggerSettings settings, CpuData[] cpuData, int seed)
        {
            EventCounterTriggerImpl trigger = new(settings);

            CpuUsagePayloadFactory payloadFactory = new(seed, settings.CounterIntervalSeconds);

            for (int i = 0; i < cpuData.Length; i++)
            {
                ref CpuData data = ref cpuData[i];
                _output.WriteLine("Data: Value={0}, Expected={1}, Drop={2}", data.Value, data.Result, data.Drop);
                ICounterPayload payload = payloadFactory.CreateNext(data.Value);
                if (data.Drop)
                {
                    continue;
                }
                Assert.Equal(data.Result, trigger.HasSatisfiedCondition(payload));
            }
        }

        private sealed class CpuData
        {
            public CpuData(double value, bool result, bool drop = false)
            {
                Drop = drop;
                Result = result;
                Value = value;
            }

            /// <summary>
            /// Specifies if the data should be "dropped" to simulate dropping of events.
            /// </summary>
            public bool Drop { get; }

            /// <summary>
            /// The expected result of evaluating the trigger on this data.
            /// </summary>
            public bool Result { get;}

            /// <summary>
            /// The sample CPU value to be given to the trigger for evaluation.
            /// </summary>
            public double Value { get; }
        }

        /// <summary>
        /// Creates CPU Usage payloads in successive order, simulating the data produced
        /// for the cpu-usage counter from the runtime.
        /// </summary>
        private sealed class CpuUsagePayloadFactory
        {
            private readonly int _intervalSeconds;
            private readonly Random _random;

            private DateTime? _lastTimestamp;

            public CpuUsagePayloadFactory(int seed, int intervalSeconds)
            {
                _intervalSeconds = intervalSeconds;
                _random = new Random(seed);
            }

            /// <summary>
            /// Creates the next counter payload based on the provided value.
            /// </summary>
            /// <remarks>
            /// The timestamp is roughly incremented by the specified interval from the constructor
            /// in order to simulate variations in the timestamp of counter events as seen in real
            /// event data. The actual interval is also roughly generated from specified interval
            /// from the constructor to simulate variations in the collection interval as seen in
            /// real event data.
            /// </remarks>
            public ICounterPayload CreateNext(double value)
            {
                // Add some variance between 0 to 10 milliseconds to simulate real interval value.
                float actualInterval = Convert.ToSingle(_intervalSeconds + _random.NextDouble() / 100);

                if (!_lastTimestamp.HasValue)
                {
                    // Start with the current time
                    _lastTimestamp = DateTime.UtcNow;
                }
                else
                {
                    // Increment timestamp by one whole interval
                    _lastTimestamp = _lastTimestamp.Value.AddSeconds(actualInterval);
                }

                // Add some variance between 0 and 10 milliseconds to simulate real timestamp
                _lastTimestamp = _lastTimestamp.Value.AddMilliseconds(10 * _random.NextDouble());

                return new CounterPayload(
                    _lastTimestamp.Value,
                    EventCounterConstants.RuntimeProviderName,
                    EventCounterConstants.CpuUsageCounterName,
                    EventCounterConstants.CpuUsageDisplayName,
                    EventCounterConstants.CpuUsageUnits,
                    value,
                    CounterType.Metric,
                    actualInterval);
            }
        }
    }
}
