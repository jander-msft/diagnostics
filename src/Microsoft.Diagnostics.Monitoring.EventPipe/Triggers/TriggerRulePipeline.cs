using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class TriggerRulePipeline : EventSourcePipeline<EventSourcePipelineSettings>
    {
        private readonly ITriggerRuleContext _context;
        private readonly ITriggerRule _rule;

        public TriggerRulePipeline(DiagnosticsClient client, TriggerRulePipelineSettings settings)
            : base(client, settings)
        {
            if (null == settings.RuleFactory)
            {
                throw new ArgumentException(null, nameof(settings.RuleFactory));
            }

            _context = new TriggerRuleContext();
            _rule = settings.RuleFactory.Create(_context);
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            return _rule.CreateConfiguration();
        }

        protected override Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            _rule.RegisterCallbacks(eventSource);

            return Task.CompletedTask;
        }

        private class TriggerRuleContext : ITriggerRuleContext
        {
            public Task NotifyTriggerAsync(string triggerName, CancellationToken token)
            {
                Debug.WriteLine($"Trigger: {triggerName}");
                return Task.CompletedTask;
            }

            public MonitoringSourceConfiguration Configuration { get; private set; }
        }
    }
}
