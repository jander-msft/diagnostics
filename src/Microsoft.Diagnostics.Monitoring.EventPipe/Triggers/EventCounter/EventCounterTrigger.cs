// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    internal sealed class EventCounterTrigger :
        IEventTrigger<TraceEvent>
    {
        private readonly Queue<Tuple<DateTime, bool>> _backlog;
        private readonly int _targetCount;
        private readonly CounterFilter _counterFilter;
        private readonly Func<double, bool> _valueFilter;

        public EventCounterTrigger(EventCounterTriggerSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _counterFilter = new CounterFilter(settings.CounterIntervalSeconds);
            _counterFilter.AddFilter(settings.ProviderName, new string[] { settings.CounterName });

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

            _backlog = new Queue<Tuple<DateTime, bool>>(_targetCount);
        }

        public bool HasSatisfiedCondition(TraceEvent traceEvent)
        {
            if (traceEvent.TryGetCounterPayload(_counterFilter, out ICounterPayload payload))
            {
                // Prevent queue from growing larger than the target count.
                if (_backlog.Count == _targetCount)
                {
                    _backlog.Dequeue();
                }

                _backlog.Enqueue(Tuple.Create(traceEvent.TimeStamp, _valueFilter(payload.Value)));

                if (_backlog.Count == _targetCount &&
                    _backlog.All(t => t.Item2))
                {
                    return true;
                }
            }
            return false;
        }

        public static MonitoringSourceConfiguration CreateConfiguration(EventCounterTriggerSettings settings)
        {
            return new MetricSourceConfiguration(settings.CounterIntervalSeconds, new string[] { settings.ProviderName });
        }
    }
}
