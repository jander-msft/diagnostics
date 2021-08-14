// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Pipelines
{
    internal sealed class EventPipeTriggerPipelineSettings<TSettings> :
        EventSourcePipelineSettings
    {
        public MonitoringSourceConfiguration Configuration { get; set; }

        public ITraceEventTriggerFactory<TSettings> TriggerFactory { get; set; }

        public TSettings TriggerSettings { get; set; }
    }
}
