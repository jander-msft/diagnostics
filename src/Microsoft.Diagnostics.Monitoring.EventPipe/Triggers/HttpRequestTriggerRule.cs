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

        public async Task InitializeAsync(ITriggerRuleContext context, CancellationToken token)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

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
            Debug.WriteLine("Start Event");
            foreach (string payloadName in traceEvent.PayloadNames)
            {
                if ("Arguments" == payloadName)
                {
                    IDictionary<string, string> arguments = traceEvent.GetDiagnosticSourceTraceEventArguments();
                    foreach (KeyValuePair<string, string> keyvalue in arguments)
                    {
                        Debug.WriteLine($"Argument: {keyvalue.Key} = {keyvalue.Value}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Payload: {payloadName} = {traceEvent.PayloadByName(payloadName)}");
                }
            }
        }

        private void HttpRequestInStop(TraceEvent traceEvent)
        {
            Debug.WriteLine("End Event");
            foreach (string payloadName in traceEvent.PayloadNames)
            {
                Debug.WriteLine($"Payload: {payloadName} = {traceEvent.PayloadByName(payloadName)}");
            }
        }
    }
}
