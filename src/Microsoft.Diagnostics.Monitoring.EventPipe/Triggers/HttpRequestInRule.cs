using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class HttpRequestInRule : ITriggerRule
    {
        private const string ActivityIdArgumentName = "ActivityId";

        private readonly TimeSpan _threshold = TimeSpan.FromSeconds(1);

        private ITriggerRuleContext _context;
        private bool _hasRemainingRequests = false;
        private IDictionary<string, DateTime> _requestMap =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public HttpRequestInRule(ITriggerRuleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public MonitoringSourceConfiguration CreateConfiguration()
        {
            return new HttpRequestSourceConfiguration();
        }

        public void RegisterCallbacks(TraceEventSource eventSource)
        {
            if (null == eventSource)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            eventSource.Dynamic.AddCallbackForProviderEvent(
                MonitoringSourceConfiguration.DiagnosticSourceEventSource,
                "Activity1/Start",
                HttpRequestInStart);
            eventSource.Dynamic.AddCallbackForProviderEvent(
                MonitoringSourceConfiguration.DiagnosticSourceEventSource,
                "Activity1/Stop",
                HttpRequestInStop);
        }

        public async Task StopAsync(CancellationToken token)
        {
            if (_hasRemainingRequests)
            {
                await _context.NotifyTriggerAsync(HttpRequestTriggerNames.RemainingRequests, token);
            }
        }

        private void HttpRequestInStart(TraceEvent traceEvent)
        {
            var arguments = traceEvent.GetDiagnosticSourceTraceEventArguments();

            if (arguments.TryGetValue(ActivityIdArgumentName, out string activityId))
            {
                lock (_requestMap)
                {
                    _requestMap.Add(activityId, traceEvent.TimeStamp);
                    _hasRemainingRequests = true;
                }
            }
        }

        private void HttpRequestInStop(TraceEvent traceEvent)
        {
            var arguments = traceEvent.GetDiagnosticSourceTraceEventArguments();

            if (arguments.TryGetValue(ActivityIdArgumentName, out string activityId))
            {
                bool hasRemainingRequests = false;
                DateTime? startTimestamp = null;

                lock (_requestMap)
                {
                    if (_requestMap.TryGetValue(activityId, out DateTime timestamp))
                    {
                        startTimestamp = timestamp;
                        _requestMap.Remove(activityId);
                    }
                    hasRemainingRequests = (_hasRemainingRequests = _requestMap.Count > 0);
                }

                if (startTimestamp.HasValue && traceEvent.TimeStamp - startTimestamp.Value > _threshold)
                {
                    string triggerName = hasRemainingRequests ?
                        HttpRequestTriggerNames.OverThresholdRemainingRequests :
                        HttpRequestTriggerNames.OverThreshold;

                    _context.NotifyTriggerAsync(triggerName, CancellationToken.None);
                }
            }
        }
    }
}
