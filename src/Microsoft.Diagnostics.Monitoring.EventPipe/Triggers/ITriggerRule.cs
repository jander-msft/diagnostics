using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System.Collections.Generic;
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
        Task SetProvidersAsync(IEnumerable<EventPipeProvider> providers, CancellationToken token);

        Task EgressEventsAsync(string providerName, CancellationToken token);
    }
}
