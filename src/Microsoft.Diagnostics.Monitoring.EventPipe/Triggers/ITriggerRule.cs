using Microsoft.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal interface ITriggerRule
    {
        Task InitializeAsync(ITriggerRuleContext context, CancellationToken token);

        void SetupCallbacks(TraceEventSource eventSource);
    }

    internal interface ITriggerRuleContext
    {
        Task SetConfigurationAsync(MonitoringSourceConfiguration configuration, CancellationToken token);

        Task NotifyTrigger(string triggerName, CancellationToken token);
    }
}
