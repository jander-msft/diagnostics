using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class TriggerRulePipelineSettings : EventSourcePipelineSettings
    {
        public ITriggerRuleFactory RuleFactory { get; set; }
    }
}
