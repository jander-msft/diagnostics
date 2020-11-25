using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class HttpRequestInRule : ITriggerRule
    {
        private ITriggerRuleContext _context;
        private IDictionary<string, DateTime> _dueTime;

        public async Task InitializeAsync(ITriggerRuleContext context, CancellationToken token)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _dueTime = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            await context.SetConfigurationAsync(new HttpRequestSourceConfiguration(), token);
        }

        public void SetupCallbacks(TraceEventSource eventSource)
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

        private void HttpRequestInStart(TraceEvent traceEvent)
        {
            var arguments = traceEvent.GetDiagnosticSourceTraceEventArguments();
            string activityId = arguments["ActivityId"];

            Debug.WriteLine($"Adding HTTP activity {activityId} to map.");

            _dueTime.Add(arguments["ActivityId"], traceEvent.TimeStamp.AddSeconds(1));
        }

        private void HttpRequestInStop(TraceEvent traceEvent)
        {
            var arguments = traceEvent.GetDiagnosticSourceTraceEventArguments();
            string activityId = arguments["ActivityId"];

            if (_dueTime.TryGetValue(activityId, out DateTime dueTime))
            {
                Debug.WriteLine($"Removing HTTP activity {activityId} from map.");

                _dueTime.Remove(activityId);

                if (dueTime <= traceEvent.TimeStamp)
                {
                    _context.NotifyTrigger("Trigger!", CancellationToken.None);
                }
            }
        }
    }
}
