// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    // The core implementation of the EventCounter trigger that processes
    // the trigger settings and evaluates the counter payload. Primary motivation
    // for the implementation is for unit testability separate from TraceEvent.
    internal sealed class EventCounterTriggerImpl
    {
        private readonly int _intervalSeconds;
        private readonly Func<double, bool> _valueFilter;
        private readonly TimeSpan _window;

        private DateTime? _latestTimestamp;
        private DateTime? _targetTimestamp;

        public CounterFilter Filter { get; }

        public EventCounterTriggerImpl(EventCounterTriggerSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Filter = new CounterFilter(settings.CounterIntervalSeconds);
            Filter.AddFilter(settings.ProviderName, new string[] { settings.CounterName });

            if (settings.GreaterThan.HasValue)
            {
                double minValue = settings.GreaterThan.Value;
                if (settings.LessThan.HasValue)
                {
                    double maxValue = settings.LessThan.Value;
                    _valueFilter = value => value > minValue && value < maxValue;
                }
                else
                {
                    _valueFilter = value => value > minValue;
                }
            }
            else if (settings.LessThan.HasValue)
            {
                double maxValue = settings.LessThan.Value;
                _valueFilter = value => value < maxValue;
            }

            _intervalSeconds = settings.CounterIntervalSeconds;
            _window = settings.SlidingWindowDuration;
        }

        public bool HasSatisfiedCondition(ICounterPayload payload)
        {
            if (!_valueFilter(payload.Value))
            {
                _latestTimestamp = null;
                _targetTimestamp = null;
                return false;
            }
            else if (!_targetTimestamp.HasValue)
            {
                _latestTimestamp = payload.Timestamp;
                _targetTimestamp = payload.Timestamp
                    .AddSeconds(-payload.Interval)
                    .Add(_window);
            }
            else if (_latestTimestamp.Value.AddSeconds(1.5 * _intervalSeconds) < payload.Timestamp)
            {
                _latestTimestamp = payload.Timestamp;
                _targetTimestamp = payload.Timestamp
                    .AddSeconds(-payload.Interval)
                    .Add(_window);
            }
            else
            {
                _latestTimestamp = payload.Timestamp;
            }

            return _latestTimestamp >= _targetTimestamp;
        }
    }
}
