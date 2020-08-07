﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.NETCore.Client
{
    internal abstract class IpcEndpoint
    {
        /// <summary>
        /// Connects to the underlying IPC transport and opens a read/write-able Stream
        /// </summary>
        /// <param name="timeout">The amount of time to block attempting to connect</param>
        /// <returns>A Stream for writing and reading data to and from the target .NET process</returns>
        public abstract Stream Connect(TimeSpan timeout);

        /// <summary>
        /// Wait for an available diagnostic endpoint to the runtime instance.
        /// </summary>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task the completes when a diagnostic endpoint to the runtime instance becomes available.
        /// </returns>
        public abstract Task WaitForConnectionAsync(CancellationToken token);
    }

    internal class ListenModeIpcEndpoint : IpcEndpoint
    {
        private readonly Guid _runtimeId;
        private readonly DiagnosticPortListener _listener;

        public ListenModeIpcEndpoint(DiagnosticPortListener listener, Guid runtimeId)
        {
            _runtimeId = runtimeId;
            _listener = listener;
        }

        /// <remarks>
        /// This will block until the diagnostic stream is provided. This block can happen if
        /// the stream is acquired previously and the runtime instance has not yet reconnected
        /// to the reversed diagnostics server.
        /// </remarks>
        public override Stream Connect(TimeSpan timeout)
        {
            return _listener.Connect(_runtimeId, timeout);
        }

        public override async Task WaitForConnectionAsync(CancellationToken token)
        {
            await _listener.WaitForConnectionAsync(_runtimeId, token).ConfigureAwait(false);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ListenModeIpcEndpoint);
        }

        public bool Equals(ListenModeIpcEndpoint other)
        {
            return other != null && other._runtimeId == _runtimeId && other._listener == _listener;
        }

        public override int GetHashCode()
        {
            return _runtimeId.GetHashCode() ^ _listener.GetHashCode();
        }
    }

    internal class ConnectModeIpcEndpoint : IpcEndpoint
    {
        public static string IpcRootPath { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\.\pipe\" : Path.GetTempPath();
        public static string DiagnosticsPortPattern { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"^dotnet-diagnostic-(\d+)$" : @"^dotnet-diagnostic-(\d+)-(\d+)-socket$";

        private int _pid;

        /// <summary>
        /// Creates a reference to a .NET process's IPC Transport
        /// using the default rules for a given pid
        /// </summary>
        /// <param name="pid">The pid of the target process</param>
        /// <returns>A reference to the IPC Transport</returns>
        public ConnectModeIpcEndpoint(int pid)
        {
            _pid = pid;
        }

        public override Stream Connect(TimeSpan timeout)
        {
            string address = GetDefaultAddress();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var namedPipe = new NamedPipeClientStream(
                    ".",
                    address,
                    PipeDirection.InOut,
                    PipeOptions.None,
                    TokenImpersonationLevel.Impersonation);
                namedPipe.Connect((int)timeout.TotalMilliseconds);
                return namedPipe;
            }
            else
            {
                var socket = new UnixDomainSocket();
                socket.Connect(Path.Combine(IpcRootPath, address), timeout);
                return new ExposedSocketNetworkStream(socket, ownsSocket: true);
            }
        }

        public override async Task WaitForConnectionAsync(CancellationToken token)
        {
            using var _ = await ConnectStreamAsync(token).ConfigureAwait(false);
        }

        async Task<Stream> ConnectStreamAsync(CancellationToken token)
        {
            string address = GetDefaultAddress();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var namedPipe = new NamedPipeClientStream(
                    ".",
                    address,
                    PipeDirection.InOut,
                    PipeOptions.None,
                    TokenImpersonationLevel.Impersonation);
                await namedPipe.ConnectAsync(token).ConfigureAwait(false);
                return namedPipe;
            }
            else
            {
                var socket = new UnixDomainSocket();
                await socket.ConnectAsync(Path.Combine(IpcRootPath, address), token).ConfigureAwait(false);
                return new ExposedSocketNetworkStream(socket, ownsSocket: true);
            }
        }

        private string GetDefaultAddress()
        {
            try
            {
                var process = Process.GetProcessById(_pid);
            }
            catch (ArgumentException)
            {
                throw new ServerNotAvailableException($"Process {_pid} is not running.");
            }
            catch (InvalidOperationException)
            {
                throw new ServerNotAvailableException($"Process {_pid} seems to be elevated.");
            }

            if (!TryGetDefaultAddress(_pid, out string transportName))
            {
                throw new ServerNotAvailableException($"Process {_pid} not running compatible .NET runtime.");
            }

            return transportName;
        }

        private static bool TryGetDefaultAddress(int pid, out string defaultAddress)
        {
            defaultAddress = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                defaultAddress = $"dotnet-diagnostic-{pid}";
            }
            else
            {
                try
                {
                    defaultAddress = Directory.GetFiles(IpcRootPath, $"dotnet-diagnostic-{pid}-*-socket") // Try best match.
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .FirstOrDefault();
                }
                catch (InvalidOperationException)
                {
                }
            }

            return !string.IsNullOrEmpty(defaultAddress);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ConnectModeIpcEndpoint);
        }

        public bool Equals(ConnectModeIpcEndpoint other)
        {
            return other != null && other._pid == _pid;
        }

        public override int GetHashCode()
        {
            return _pid.GetHashCode();
        }
    }
}