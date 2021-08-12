// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class EventPipeTriggerPipelineSettings<TOptions> : EventSourcePipelineSettings
    {
        public MonitoringSourceConfiguration Configuration { get; set; }

        public IEventTriggerFactory<TraceEvent, TOptions> TriggerFactory { get; set; }

        public TOptions TriggerOptions { get; set; }
    }
}
