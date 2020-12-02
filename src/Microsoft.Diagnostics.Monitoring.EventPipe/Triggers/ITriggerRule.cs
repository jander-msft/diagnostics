// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal interface ITriggerRule
    {
        MonitoringSourceConfiguration CreateConfiguration();

        void RegisterCallbacks(TraceEventSource eventSource);

        Task StopAsync(CancellationToken token);
    }

    internal interface ITriggerRuleContext
    {
        Task NotifyTriggerAsync(string triggerName, CancellationToken token);
    }
}
