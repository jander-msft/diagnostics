using System;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal interface IEventTriggerEventSource<TEvent>
    {
        int AddCallback(
            string providerName,
            string eventName,
            Action<TEvent> callback);

        void RemoveCallback(int id);
    }
}
