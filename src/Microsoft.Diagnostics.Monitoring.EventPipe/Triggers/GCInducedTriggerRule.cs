using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class GCTriggerRule : ITriggerRule
    {
        private ITriggerRuleContext _context;
        private State _state;

        public async Task InitializeAsync(ITriggerRuleContext context, CancellationToken token)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            await context.SetProvidersAsync(null, token);

            _state = State.MonitorInducedGCs;
        }

        public void SetupCallbacks(TraceEventSource eventSource)
        {
            if (null == eventSource)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            switch (_state)
            {
                case State.MonitorInducedGCs:
                    eventSource.Clr.GCTriggered += Clr_GCTriggered;
                    break;
            }
        }

        private void Clr_GCTriggered(GCTriggeredTraceData obj)
        {
            throw new NotImplementedException();
        }

        private enum State
        {
            MonitorInducedGCs
        }
    }
}
