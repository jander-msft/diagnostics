// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    // The core implementation of the EventCounter trigger that processes
    // the trigger settings and evaluates the counter payload. Primary motivation
    // for the implementation is for unit testability separate from TraceEvent.
    internal sealed class EventCounterTriggerImpl
    {
        private readonly Queue<DateTime> _backlog;
        private readonly int _targetCount;
        private readonly Func<double, bool> _valueFilter;
        private TimeSpan _window;

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

            _targetCount = Convert.ToInt32(Math.Floor(
                settings.SlidingWindowDuration.TotalSeconds / settings.CounterIntervalSeconds));

            _backlog = new Queue<DateTime>(_targetCount);
            _window = settings.SlidingWindowDuration;
        }

        public bool HasSatisfiedCondition(ICounterPayload payload)
        {
            // Prevent queue from growing larger than the target count.
            if (_backlog.Count == _targetCount)
            {
                _backlog.Dequeue();
            }

            if (_valueFilter(payload.Value))
            {
                _backlog.Enqueue(payload.Timestamp);

                if (_backlog.Count == _targetCount)
                {
                    DateTime oldestAllowedTimestamp = payload.Timestamp - _window;

                    return _backlog.All(t => t > oldestAllowedTimestamp);
                }
            }

            return false;
        }
    }
}
