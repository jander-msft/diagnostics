// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.NETCore.Client
{
    /// <summary>
    /// This is a top-level class that contains methods to send various diagnostics command to the runtime.
    /// </summary>
    public sealed class DiagnosticsClient
    {
        private readonly IpcEndpoint _endpoint;

        public DiagnosticsClient(int processId) :
            this(new PidIpcEndpoint(processId))
        {
        }

        internal DiagnosticsClient(IpcEndpointConfig config) :
            this(new DiagnosticPortIpcEndpoint(config))
        {
        }

        internal DiagnosticsClient(IpcEndpoint endpoint)
        {
            _endpoint = endpoint;
        }

        /// <summary>
        /// Wait for an available diagnostic endpoint to the runtime instance.
        /// </summary>
        /// <param name="timeout">The amount of time to wait before cancelling the wait for the connection.</param>
        internal void WaitForConnection(TimeSpan timeout)
        {
            _endpoint.WaitForConnection(timeout);
        }

        /// <summary>
        /// Wait for an available diagnostic endpoint to the runtime instance.
        /// </summary>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task the completes when a diagnostic endpoint to the runtime instance becomes available.
        /// </returns>
        internal Task WaitForConnectionAsync(CancellationToken token)
        {
            return _endpoint.WaitForConnectionAsync(token);
        }

        /// <summary>
        /// Start tracing the application and return an EventPipeSession object
        /// </summary>
        /// <param name="providers">An IEnumerable containing the list of Providers to turn on.</param>
        /// <param name="requestRundown">If true, request rundown events from the runtime</param>
        /// <param name="circularBufferMB">The size of the runtime's buffer for collecting events in MB</param>
        /// <returns>
        /// An EventPipeSession object representing the EventPipe session that just started.
        /// </returns> 
        public EventPipeSession StartEventPipeSession(IEnumerable<EventPipeProvider> providers, bool requestRundown = true, int circularBufferMB = 256)
        {
            return EventPipeSession.Start(_endpoint, providers, requestRundown, circularBufferMB);
        }

        /// <summary>
        /// Start tracing the application and return an EventPipeSession object
        /// </summary>
        /// <param name="provider">An EventPipeProvider to turn on.</param>
        /// <param name="requestRundown">If true, request rundown events from the runtime</param>
        /// <param name="circularBufferMB">The size of the runtime's buffer for collecting events in MB</param>
        /// <returns>
        /// An EventPipeSession object representing the EventPipe session that just started.
        /// </returns> 
        public EventPipeSession StartEventPipeSession(EventPipeProvider provider, bool requestRundown = true, int circularBufferMB = 256)
        {
            return EventPipeSession.Start(_endpoint, new[] { provider }, requestRundown, circularBufferMB);
        }

        /// <summary>
        /// Start tracing the application and return an EventPipeSession object
        /// </summary>
        /// <param name="providers">An IEnumerable containing the list of Providers to turn on.</param>
        /// <param name="requestRundown">If true, request rundown events from the runtime</param>
        /// <param name="circularBufferMB">The size of the runtime's buffer for collecting events in MB</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// An EventPipeSession object representing the EventPipe session that just started.
        /// </returns> 
        internal Task<EventPipeSession> StartEventPipeSessionAsync(IEnumerable<EventPipeProvider> providers, bool requestRundown, int circularBufferMB, CancellationToken token)
        {
            return EventPipeSession.StartAsync(_endpoint, providers, requestRundown, circularBufferMB, token);
        }

        /// <summary>
        /// Start tracing the application and return an EventPipeSession object
        /// </summary>
        /// <param name="provider">An EventPipeProvider to turn on.</param>
        /// <param name="requestRundown">If true, request rundown events from the runtime</param>
        /// <param name="circularBufferMB">The size of the runtime's buffer for collecting events in MB</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// An EventPipeSession object representing the EventPipe session that just started.
        /// </returns>
        internal Task<EventPipeSession> StartEventPipeSessionAsync(EventPipeProvider provider, bool requestRundown, int circularBufferMB, CancellationToken token)
        {
            return EventPipeSession.StartAsync(_endpoint, new[] { provider }, requestRundown, circularBufferMB, token);
        }

        /// <summary>
        /// Trigger a core dump generation.
        /// </summary> 
        /// <param name="dumpType">Type of the dump to be generated</param>
        /// <param name="dumpPath">Full path to the dump to be generated. By default it is /tmp/coredump.{pid}</param>
        /// <param name="logDumpGeneration">When set to true, display the dump generation debug log to the console.</param>
        public void WriteDump(DumpType dumpType, string dumpPath, bool logDumpGeneration = false)
        {
            IpcMessage request = CreateWriteDumpMessage(dumpType, dumpPath, logDumpGeneration);
            IpcMessage response = IpcClient.SendMessage(_endpoint, request);
            ValidateResponseMessage(response, nameof(WriteDump));
        }

        /// <summary>
        /// Trigger a core dump generation.
        /// </summary> 
        /// <param name="dumpType">Type of the dump to be generated</param>
        /// <param name="dumpPath">Full path to the dump to be generated. By default it is /tmp/coredump.{pid}</param>
        /// <param name="logDumpGeneration">When set to true, display the dump generation debug log to the console.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        internal async Task WriteDumpAsync(DumpType dumpType, string dumpPath, bool logDumpGeneration, CancellationToken token)
        {
            IpcMessage request = CreateWriteDumpMessage(dumpType, dumpPath, logDumpGeneration);
            IpcMessage response = await IpcClient.SendMessageAsync(_endpoint, request, token).ConfigureAwait(false);
            ValidateResponseMessage(response, nameof(WriteDumpAsync));
        }

        /// <summary>
        /// Attach a profiler.
        /// </summary>
        /// <param name="attachTimeout">Timeout for attaching the profiler</param>
        /// <param name="profilerGuid">Guid for the profiler to be attached</param>
        /// <param name="profilerPath">Path to the profiler to be attached</param>
        /// <param name="additionalData">Additional data to be passed to the profiler</param>
        public void AttachProfiler(TimeSpan attachTimeout, Guid profilerGuid, string profilerPath, byte[] additionalData = null)
        {
            if (profilerGuid == null || profilerGuid == Guid.Empty)
            {
                throw new ArgumentException($"{nameof(profilerGuid)} must be a valid Guid");
            }

            if (String.IsNullOrEmpty(profilerPath))
            {
                throw new ArgumentException($"{nameof(profilerPath)} must be non-null");
            }

            byte[] serializedConfiguration = SerializePayload((uint)attachTimeout.TotalSeconds, profilerGuid, profilerPath, additionalData);
            var message = new IpcMessage(DiagnosticsServerCommandSet.Profiler, (byte)ProfilerCommandId.AttachProfiler, serializedConfiguration);
            IpcMessage response = IpcClient.SendMessage(_endpoint, message);
            switch ((DiagnosticsServerResponseId)response.Header.CommandId)
            {
                case DiagnosticsServerResponseId.Error:
                    uint hr = BitConverter.ToUInt32(response.Payload, 0);
                    if (hr == (uint)DiagnosticsIpcError.UnknownCommand)
                    {
                        throw new UnsupportedCommandException("The target runtime does not support profiler attach");
                    }
                    if (hr == (uint)DiagnosticsIpcError.ProfilerAlreadyActive)
                    {
                        throw new ProfilerAlreadyActiveException("The request to attach a profiler was denied because a profiler is already loaded");
                    }
                    throw new ServerErrorException($"Profiler attach failed (HRESULT: 0x{hr:X8})");
                case DiagnosticsServerResponseId.OK:
                    return;
                default:
                    throw new ServerErrorException($"Profiler attach failed - server responded with unknown command");
            }

            // The call to set up the pipe and send the message operates on a different timeout than attachTimeout, which is for the runtime.
            // We should eventually have a configurable timeout for the message passing, potentially either separately from the 
            // runtime timeout or respect attachTimeout as one total duration.
        }

        /// <summary>
        /// Set a profiler as the startup profiler. It is only valid to issue this command
        /// while the runtime is paused at startup.
        /// </summary>
        /// <param name="profilerGuid">Guid for the profiler to be attached</param>
        /// <param name="profilerPath">Path to the profiler to be attached</param>
        public void SetStartupProfiler(Guid profilerGuid, string profilerPath)
        {
            if (profilerGuid == null || profilerGuid == Guid.Empty)
            {
                throw new ArgumentException($"{nameof(profilerGuid)} must be a valid Guid");
            }

            if (String.IsNullOrEmpty(profilerPath))
            {
                throw new ArgumentException($"{nameof(profilerPath)} must be non-null");
            }

            byte[] serializedConfiguration = SerializePayload(profilerGuid, profilerPath);
            var message = new IpcMessage(DiagnosticsServerCommandSet.Profiler, (byte)ProfilerCommandId.StartupProfiler, serializedConfiguration);
            var response = IpcClient.SendMessage(_endpoint, message);
            switch ((DiagnosticsServerResponseId)response.Header.CommandId)
            {
                case DiagnosticsServerResponseId.Error:
                    uint hr = BitConverter.ToUInt32(response.Payload, 0);
                    if (hr == (uint)DiagnosticsIpcError.UnknownCommand)
                    {
                        throw new UnsupportedCommandException("The target runtime does not support the ProfilerStartup command.");
                    }
                    else if (hr == (uint)DiagnosticsIpcError.InvalidArgument)
                    {
                        throw new ServerErrorException("The runtime must be suspended to issue the SetStartupProfiler command.");
                    }

                    throw new ServerErrorException($"Profiler startup failed (HRESULT: 0x{hr:X8})");
                case DiagnosticsServerResponseId.OK:
                    return;
                default:
                    throw new ServerErrorException($"Profiler startup failed - server responded with unknown command");
            }
        }

        /// <summary>
        /// Tell the runtime to resume execution after being paused at startup.
        /// </summary>
        public void ResumeRuntime()
        {
            IpcMessage request = CreateResumeRuntimeMessage();
            IpcMessage response = IpcClient.SendMessage(_endpoint, request);
            ValidateResponseMessage(response, nameof(ResumeRuntime));
        }

        internal async Task ResumeRuntimeAsync(CancellationToken cancellationToken)
        {
            IpcMessage request = CreateResumeRuntimeMessage();
            IpcMessage response = await IpcClient.SendMessageAsync(_endpoint, request, cancellationToken).ConfigureAwait(false);
            ValidateResponseMessage(response, nameof(ResumeRuntimeAsync));
        }

        /// <summary>
        /// Set an environment variable in the target process.
        /// </summary>
        /// <param name="name">The name of the environment variable to set.</param>
        /// <param name="value">The value of the environment variable to set.</param>
        public void SetEnvironmentVariable(string name, string value)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"{nameof(name)} must be non-null.");
            }

            byte[] serializedConfiguration = SerializePayload(name, value);
            var message = new IpcMessage(DiagnosticsServerCommandSet.Process, (byte)ProcessCommandId.SetEnvironmentVariable, serializedConfiguration);
            var response = IpcClient.SendMessage(_endpoint, message);
            switch ((DiagnosticsServerResponseId)response.Header.CommandId)
            {
                case DiagnosticsServerResponseId.Error:
                    uint hr = BitConverter.ToUInt32(response.Payload, 0);
                    if (hr == (uint)DiagnosticsIpcError.UnknownCommand)
                    {
                        throw new UnsupportedCommandException("The target runtime does not support the SetEnvironmentVariable command.");
                    }

                    throw new ServerErrorException($"SetEnvironmentVariable failed (HRESULT: 0x{hr:X8})");
                case DiagnosticsServerResponseId.OK:
                    return;
                default:
                    throw new ServerErrorException($"SetEnvironmentVariable failed - server responded with unknown command");
            }
        }

        /// <summary>
        /// Gets all environement variables and their values from the target process.
        /// </summary>
        /// <returns>A dictionary containing all of the environment variables defined in the target process.</returns>
        public Dictionary<string, string> GetProcessEnvironment()
        {
            IpcMessage message = CreateProcessEnvironmentMessage();
            using IpcResponse response = IpcClient.SendMessageGetContinuation(_endpoint, message);
            Task<Dictionary<string, string>> envTask = GetProcessEnvironmentFromResponse(response, nameof(GetProcessEnvironment), CancellationToken.None);
            envTask.Wait();
            return envTask.Result;
        }

        internal async Task<Dictionary<string, string>> GetProcessEnvironmentAsync(CancellationToken token)
        {
            IpcMessage message = CreateProcessEnvironmentMessage();
            using IpcResponse response = await IpcClient.SendMessageGetContinuationAsync(_endpoint, message, token).ConfigureAwait(false);
            return await GetProcessEnvironmentFromResponse(response, nameof(GetProcessEnvironmentAsync), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all the active processes that can be attached to.
        /// </summary>
        /// <returns>
        /// IEnumerable of all the active process IDs.
        /// </returns>
        public static IEnumerable<int> GetPublishedProcesses()
        {
            static IEnumerable<int> GetAllPublishedProcesses()
            {
                foreach (var port in Directory.GetFiles(PidIpcEndpoint.IpcRootPath))
                {
                    var fileName = new FileInfo(port).Name;
                    var match = Regex.Match(fileName, PidIpcEndpoint.DiagnosticsPortPattern);
                    if (!match.Success) continue;
                    var group = match.Groups[1].Value;
                    if (!int.TryParse(group, NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId))
                        continue;

                    yield return processId;
                }
            }

            return GetAllPublishedProcesses().Distinct();
        }

        internal ProcessInfo GetProcessInfo()
        {
            // RE: https://github.com/dotnet/runtime/issues/54083
            // If the GetProcessInfo2 command is sent too early, it will crash the runtime instance.
            // Disable the usage of the command until that issue is fixed.

            // Attempt to get ProcessInfo v2
            //ProcessInfo processInfo = TryGetProcessInfo2();
            //if (null != processInfo)
            //{
            //    return processInfo;
            //}

            IpcMessage request = CreateProcessInfoMessage();
            using IpcResponse response = IpcClient.SendMessageGetContinuation(_endpoint, request);
            return GetProcessInfoFromResponse(response, nameof(GetProcessInfo));
        }

        internal async Task<ProcessInfo> GetProcessInfoAsync(CancellationToken token)
        {
            // RE: https://github.com/dotnet/runtime/issues/54083
            // If the GetProcessInfo2 command is sent too early, it will crash the runtime instance.
            // Disable the usage of the command until that issue is fixed.

            // Attempt to get ProcessInfo v2
            //ProcessInfo processInfo = await TryGetProcessInfo2Async(token);
            //if (null != processInfo)
            //{
            //    return processInfo;
            //}

            IpcMessage request = CreateProcessInfoMessage();
            using IpcResponse response = await IpcClient.SendMessageGetContinuationAsync(_endpoint, request, token);
            return GetProcessInfoFromResponse(response, nameof(GetProcessInfoAsync));
        }

        private ProcessInfo TryGetProcessInfo2()
        {
            IpcMessage request = CreateProcessInfo2Message();
            using IpcResponse response2 = IpcClient.SendMessageGetContinuation(_endpoint, request);
            return TryGetProcessInfo2FromResponse(response2, nameof(GetProcessInfo));
        }

        private async Task<ProcessInfo> TryGetProcessInfo2Async(CancellationToken token)
        {
            IpcMessage request = CreateProcessInfo2Message();
            using IpcResponse response2 = await IpcClient.SendMessageGetContinuationAsync(_endpoint, request, token);
            return TryGetProcessInfo2FromResponse(response2, nameof(GetProcessInfoAsync));
        }

        private static byte[] SerializePayload<T>(T arg)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializePayloadArgument(arg, writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] SerializePayload<T1, T2>(T1 arg1, T2 arg2)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializePayloadArgument(arg1, writer);
                SerializePayloadArgument(arg2, writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] SerializePayload<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializePayloadArgument(arg1, writer);
                SerializePayloadArgument(arg2, writer);
                SerializePayloadArgument(arg3, writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] SerializePayload<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializePayloadArgument(arg1, writer);
                SerializePayloadArgument(arg2, writer);
                SerializePayloadArgument(arg3, writer);
                SerializePayloadArgument(arg4, writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void SerializePayloadArgument<T>(T obj, BinaryWriter writer)
        {
            if (typeof(T) == typeof(string))
            {
                writer.WriteString((string)((object)obj));
            }
            else if (typeof(T) == typeof(int))
            {
                writer.Write((int)((object)obj));
            }
            else if (typeof(T) == typeof(uint))
            {
                writer.Write((uint)((object)obj));
            }
            else if (typeof(T) == typeof(bool))
            {
                bool bValue = (bool)((object)obj);
                uint uiValue = bValue ? (uint)1 : 0;
                writer.Write(uiValue);
            }
            else
            {
                throw new ArgumentException($"Type {obj.GetType()} is not supported in SerializePayloadArgument, please add it.");
            }
        }

        private static IpcMessage CreateProcessEnvironmentMessage()
        {
            return new IpcMessage(DiagnosticsServerCommandSet.Process, (byte)ProcessCommandId.GetProcessEnvironment);
        }

        private static IpcMessage CreateProcessInfoMessage()
        {
            return new IpcMessage(DiagnosticsServerCommandSet.Process, (byte)ProcessCommandId.GetProcessInfo);
        }

        private static IpcMessage CreateProcessInfo2Message()
        {
            return new IpcMessage(DiagnosticsServerCommandSet.Process, (byte)ProcessCommandId.GetProcessInfo2);
        }

        private static IpcMessage CreateResumeRuntimeMessage()
        {
            return new IpcMessage(DiagnosticsServerCommandSet.Process, (byte)ProcessCommandId.ResumeRuntime);
        }

        private static IpcMessage CreateWriteDumpMessage(DumpType dumpType, string dumpPath, bool logDumpGeneration)
        {
            if (string.IsNullOrEmpty(dumpPath))
                throw new ArgumentNullException($"{nameof(dumpPath)} required");

            byte[] payload = SerializePayload(dumpPath, (uint)dumpType, logDumpGeneration);
            return new IpcMessage(DiagnosticsServerCommandSet.Dump, (byte)DumpCommandId.GenerateCoreDump, payload);
        }

        private static Task<Dictionary<string, string>> GetProcessEnvironmentFromResponse(IpcResponse response, string operationName, CancellationToken token)
        {
            ValidateResponseMessage(response.Message, operationName);

            ProcessEnvironmentHelper helper = ProcessEnvironmentHelper.Parse(response.Message.Payload);
            return helper.ReadEnvironmentAsync(response.Continuation, token);
        }

        private static ProcessInfo GetProcessInfoFromResponse(IpcResponse response, string operationName)
        {
            ValidateResponseMessage(response.Message, operationName);

            return ProcessInfo.ParseV1(response.Message.Payload);
        }

        private static ProcessInfo TryGetProcessInfo2FromResponse(IpcResponse response, string operationName)
        {
            if (!ValidateResponseMessage(response.Message, operationName, ValidateResponseOptions.UnknownCommandReturnsFalse))
            {
                return null;
            }

            return ProcessInfo.ParseV2(response.Message.Payload);
        }

        internal static bool ValidateResponseMessage(IpcMessage responseMessage, string operationName, ValidateResponseOptions options = ValidateResponseOptions.None)
        {
            switch ((DiagnosticsServerResponseId)responseMessage.Header.CommandId)
            {
                case DiagnosticsServerResponseId.Error:
                    uint hr = BitConverter.ToUInt32(responseMessage.Payload, 0);
                    if (hr == (uint)DiagnosticsIpcError.UnknownCommand)
                    {
                        if (options.HasFlag(ValidateResponseOptions.UnknownCommandReturnsFalse))
                        {
                            return false;
                        }
                        throw new UnsupportedCommandException($"{operationName} failed - Command is not supported.");
                    }
                    throw new ServerErrorException($"{operationName} failed (HRESULT: 0x{hr:X8})");
                case DiagnosticsServerResponseId.OK:
                    return true;
                default:
                    throw new ServerErrorException($"{operationName} failed - server responded with unknown command.");
            }
        }

        [Flags]
        internal enum ValidateResponseOptions
        {
            None = 0x0,
            UnknownCommandReturnsFalse = 0x1
        }
    }
}
