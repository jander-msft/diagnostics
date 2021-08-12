using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class EventTriggerSubscriptionDescriptor
    {
        public string ProviderName { get; set; }

        public string EventName { get; set; }
    }
}
