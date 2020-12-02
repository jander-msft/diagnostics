using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class HttpRequestInRuleFactory : ITriggerRuleFactory
    {
        public ITriggerRule Create(ITriggerRuleContext context)
        {
            return new HttpRequestInRule(context);
        }
    }
}
