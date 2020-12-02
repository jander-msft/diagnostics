using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal interface ITriggerRuleFactory
    {
        ITriggerRule Create(ITriggerRuleContext context);
    }
}
