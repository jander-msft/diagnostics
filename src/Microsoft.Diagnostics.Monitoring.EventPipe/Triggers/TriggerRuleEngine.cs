using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class TriggerRuleEngine : Pipeline
    {
        private readonly DiagnosticsClient _client;
        private readonly ITriggerRule _rule;

        public TriggerRuleEngine(ITriggerRule rule, DiagnosticsClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        protected override Task OnRun(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                var context = new TriggerRuleContext();

                await _rule.InitializeAsync(context, token);

                TaskCompletionSource<Stream> eventStreamSource =
                    new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);

                var configuration = context.Configuration;
                using EventPipeSession session = _client.StartEventPipeSession(
                    configuration.GetProviders(),
                    configuration.RequestRundown,
                    configuration.BufferSizeInMB);

                EventPipeEventSource eventSource = new EventPipeEventSource(session.EventStream);

                _rule.SetupCallbacks(eventSource);

                eventSource.Process();
            }, token);
        }

        private class TriggerRuleContext : ITriggerRuleContext
        {
            public Task NotifyTrigger(string triggerName, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            public Task SetConfigurationAsync(MonitoringSourceConfiguration configuration, CancellationToken token)
            {
                Configuration = configuration;
                return Task.CompletedTask;
            }

            public MonitoringSourceConfiguration Configuration { get; private set; }
        }
    }
}
