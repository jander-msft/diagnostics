// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public class EventCounterPipeline : EventSourcePipeline<EventPipeCounterPipelineSettings>
    {
        private readonly IEnumerable<IMetricsLogger> _metricsLogger;
        private readonly CounterFilter _filter;

        public EventCounterPipeline(DiagnosticsClient client,
            EventPipeCounterPipelineSettings settings,
            IEnumerable<IMetricsLogger> metricsLogger) : base(client, settings)
        {
            _metricsLogger = metricsLogger ?? throw new ArgumentNullException(nameof(metricsLogger));

            if (settings.CounterGroups.Length > 0)
            {
                _filter = new CounterFilter();
                foreach (var counterGroup in settings.CounterGroups)
                {
                    _filter.AddFilter(counterGroup.ProviderName, MetricsIntervalSeconds * 1000, counterGroup.CounterNames);
                }
            }
            else
            {
                _filter = CounterFilter.AllCounters;
            }
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return new MetricSourceConfiguration(MetricsIntervalSeconds, _filter.GetProviders());
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource source, Func<Task> stopSessionAsync, CancellationToken token)
        {
            ExecuteMetricLoggerAction((metricLogger) => metricLogger.PipelineStarted());

            try
            {
                source.Dynamic.All += traceEvent =>
                {
                    try
                    {
                        // Metrics
                        if (traceEvent.TryGetCounterPayload(out IDictionary<string, object> payloadFields))
                        {
                            //Make sure we are part of the requested series. If multiple clients request metrics, all of them get the metrics.
                            if (!_filter.IsIncluded(traceEvent.ProviderName, payloadFields))
                            {
                                return;
                            }

                            string counterName = payloadFields["Name"].ToString();
                            float intervalSec = (float)payloadFields["IntervalSec"];
                            string displayName = payloadFields["DisplayName"].ToString();
                            string displayUnits = payloadFields["DisplayUnits"].ToString();
                            double value = 0;
                            MetricType metricType = MetricType.Avg;

                            if (payloadFields["CounterType"].Equals("Mean"))
                            {
                                value = (double)payloadFields["Mean"];
                            }
                            else if (payloadFields["CounterType"].Equals("Sum"))
                            {
                                metricType = MetricType.Sum;
                                value = (double)payloadFields["Increment"];
                                if (string.IsNullOrEmpty(displayUnits))
                                {
                                    displayUnits = "count";
                                }
                                //TODO Should we make these /sec like the dotnet-counters tool?
                            }

                            // Note that dimensional data such as pod and namespace are automatically added in prometheus and azure monitor scenarios.
                            // We no longer added it here.
                            var metric = new Metric(traceEvent.TimeStamp,
                                traceEvent.ProviderName,
                                counterName, displayName,
                                displayUnits,
                                value,
                                metricType,
                                intervalSec);

                            ExecuteMetricLoggerAction((metricLogger) => metricLogger.LogMetrics(metric));
                        }
                    }
                    catch (Exception)
                    {
                    }
                };

                using var sourceCompletedTaskSource = new EventTaskSource<Action>(
                    taskComplete => taskComplete,
                    handler => source.Completed += handler,
                    handler => source.Completed -= handler,
                    token);

                await sourceCompletedTaskSource.Task;
            }
            finally
            {
                ExecuteMetricLoggerAction((metricLogger) => metricLogger.PipelineStopped());
            }
        }


        protected async override ValueTask OnDispose()
        {
            foreach (IMetricsLogger logger in _metricsLogger)
            {
                if (logger is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    logger?.Dispose();
                }
            }

            await base.OnDispose();
        }

        private void ExecuteMetricLoggerAction(Action<IMetricsLogger> action)
        {
            foreach (IMetricsLogger metricLogger in _metricsLogger)
            {
                try
                {
                    action(metricLogger);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private int MetricsIntervalSeconds => (int)Settings.RefreshInterval.TotalSeconds;
    }
}
