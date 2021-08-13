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

        [Fact]
        public void EventCounterTriggerGreaterThanTest()
        {
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

        [Fact]
        public void EventCounterTriggerLessThanTest()
        {
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

        [Fact]
        public void EventCounterTriggerRangeTest()
        {
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

        [Fact]
        public void EventCounterTriggerDropTest()
        {
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

        private void SimulateDataVerifyTrigger(EventCounterTriggerSettings settings, CpuData[] cpuData)
        {
            EventCounterTriggerImpl trigger = new(settings);

            CpuUsagePayloadFactory payloadFactory = new(settings.CounterIntervalSeconds);

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

            public bool Drop { get; }

            public bool Result { get;}

            public double Value { get; }
        }

        private sealed class CpuUsagePayloadFactory
        {
            private readonly int _intervalSeconds;
            private readonly Random _randomIntervalOffset =
                new Random();
            private readonly Random _randomTimestampOffset =
                new Random();

            private DateTime? _lastTimestamp;

            public CpuUsagePayloadFactory(int intervalSeconds)
            {
                _intervalSeconds = intervalSeconds;
            }

            public ICounterPayload CreateNext(double value)
            {
                // Add some variance between 0 to 10 milliseconds to simulate real actual interval value.
                float actualInterval = Convert.ToSingle(_intervalSeconds + _randomIntervalOffset.NextDouble() / 100);

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

                // Add some variance between 0 and 10 milliseconds to simulate real actual timestamp
                _lastTimestamp = _lastTimestamp.Value.AddMilliseconds(10 * _randomTimestampOffset.NextDouble());

                return CreateCpuUsagePayload(value, _lastTimestamp.Value, actualInterval);
            }

            private static ICounterPayload CreateCpuUsagePayload(double value, DateTime timestamp, float actualInterval)
            {
                return new CounterPayload(
                    timestamp,
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
