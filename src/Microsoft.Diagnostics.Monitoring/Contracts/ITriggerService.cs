using Microsoft.Diagnostics.NETCore.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring
{
    public interface ITriggerService
    {
    }

    internal interface ITriggerServiceInternal : ITriggerService
    {
        Task RegisterAsync(IpcEndpointInfo info, CancellationToken token);
    }
}
