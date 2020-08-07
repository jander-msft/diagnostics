﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Monitoring;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Options;

namespace Microsoft.Diagnostics.Tools.Monitor
{
    /// <summary>
    /// Wraps an <see cref="IEndpointInfoSource"/> based on the provided configuration
    /// and filters the current process from consideration.
    /// </summary>
    internal class FilteredEndpointInfoSource : IEndpointInfoSourceInternal, IAsyncDisposable
    {
        private readonly DiagnosticPortConfiguration _configuration;
        private readonly int? _processIdToFilterOut;
        private readonly Guid? _runtimeInstanceCookieToFilterOut;
        private readonly IEndpointInfoSourceInternal _source;

        public FilteredEndpointInfoSource(IOptions<DiagnosticPortConfiguration> configuration)
        {
            _configuration = configuration.Value;
            switch (_configuration.ConnectionMode)
            {
                case DiagnosticPortConnectionMode.Connect:
                    _source = new ConnectModeEndpointInfoSource();
                    break;
                case DiagnosticPortConnectionMode.Listen:
                    _source = new ListenModeEndpointInfoSource(_configuration.EndpointName);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled connection mode: {_configuration.ConnectionMode}");
            }

            // Filter out the current process based on the connection mode.
            if (RuntimeInfo.IsDiagnosticsEnabled)
            {
                int pid = Process.GetCurrentProcess().Id;

                // Regardless of connection mode, can use the runtime instance cookie to filter self out.
                try
                {
                    var client = new DiagnosticsClient(pid);
                    Guid runtimeInstanceCookie = client.GetProcessInfo().RuntimeInstanceCookie;
                    if (Guid.Empty != runtimeInstanceCookie)
                    {
                        _runtimeInstanceCookieToFilterOut = runtimeInstanceCookie;
                    }
                }
                catch (Exception)
                {
                }

                // If connecting to runtime instances, filter self out. In listening mode, it's likely
                // that multiple processes have the same PID in multi-container scenarios.
                if (DiagnosticPortConnectionMode.Connect == configuration.Value.ConnectionMode)
                {
                    _processIdToFilterOut = pid;
                }
            }
        }

        public async Task<IEnumerable<IEndpointInfo>> GetEndpointInfoAsync(CancellationToken token)
        {
            var endpointInfos = await _source.GetEndpointInfoAsync(token);

            // Apply process ID filter
            if (_processIdToFilterOut.HasValue)
            {
                endpointInfos = endpointInfos.Where(info => info.ProcessId != _processIdToFilterOut.Value);
            }

            // Apply runtime instance cookie filter
            if (_runtimeInstanceCookieToFilterOut.HasValue)
            {
                endpointInfos = endpointInfos.Where(info => info.RuntimeInstanceCookie != _runtimeInstanceCookieToFilterOut.Value);
            }

            return endpointInfos;
        }

        public async ValueTask DisposeAsync()
        {
            if (_source is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (_source is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.ConfigureAwait(false).DisposeAsync();
            }
        }

        public void Start()
        {
            if (_source is ListenModeEndpointInfoSource source)
            {
                source.Start(_configuration.MaxConnections.GetValueOrDefault(DiagnosticPortListener.MaxAllowedConnections));
            }
        }
    }
}
