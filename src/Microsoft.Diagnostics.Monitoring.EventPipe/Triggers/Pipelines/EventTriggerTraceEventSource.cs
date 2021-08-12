using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class EventTriggerTraceEventSource :
        IEventTriggerEventSource<TraceEvent>
    {
        private readonly IDictionary<int, Action<TraceEvent>> _callbacks
            = new Dictionary<int, Action<TraceEvent>>();

        private readonly TraceEventSource _eventSource;

        private int _nextRegistrationId;

        public EventTriggerTraceEventSource(TraceEventSource eventSource)
        {
            _eventSource = eventSource;
        }

        public int AddCallback(string providerName, string eventName, Action<TraceEvent> callback)
        {
            int registrationId = _nextRegistrationId++;

            _eventSource.Dynamic.AddCallbackForProviderEvent(providerName, eventName, callback);

            _callbacks.Add(registrationId, callback);

            return registrationId;
        }

        public void RemoveCallback(int id)
        {
            _eventSource.Dynamic.RemoveCallback(_callbacks[id]);
        }
    }
}
