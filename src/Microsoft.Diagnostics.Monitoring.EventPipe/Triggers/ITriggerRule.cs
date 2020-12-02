using Microsoft.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal interface ITriggerRule
    {
        MonitoringSourceConfiguration CreateConfiguration();

        void RegisterCallbacks(TraceEventSource eventSource);

        Task StopAsync(CancellationToken token);
    }

    internal interface ITriggerRuleContext
    {
        Task NotifyTriggerAsync(string triggerName, CancellationToken token);
    }
}
