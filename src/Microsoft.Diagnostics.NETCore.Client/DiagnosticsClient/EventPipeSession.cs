// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.NETCore.Client
{
    public class EventPipeSession : IDisposable
    {
        private ulong _sessionId;
        private IpcEndpoint _endpoint;
        private bool _disposedValue; // To detect redundant calls
        private bool _stopped; // To detect redundant calls
        private readonly IpcResponse _response;

        private EventPipeSession(IpcEndpoint endpoint, IpcResponse response, ulong sessionId)
        {
            _endpoint = endpoint;
            _response = response;
            _sessionId = sessionId;
        }

        public Stream EventStream => _response.Continuation;

        internal static EventPipeSession Start(IpcEndpoint endpoint, EventPipeSessionConfiguration config)
        {
            IpcResponse? response = null;
            try
            {
                IpcMessage requestMessage;

                // Only attempt v4 command if custom rundown keywords are provided.
                if (config.AdditionalRundownKeywords != 0)
                {
                    requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing4);
                    response = IpcClient.SendMessageGetContinuation(endpoint, requestMessage);

                    if (DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(Start), DiagnosticsClient.ValidateResponseOptions.UnknownCommandReturnsFalse))
                    {
                        return CreateSessionFromResponse(endpoint, ref response);
                    }
                }

                // Only attempt v3 command if stack walk is not requested
                if (!config.RequestStackwalk)
                {
                    requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing3);
                    response = IpcClient.SendMessageGetContinuation(endpoint, requestMessage);

                    if (DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(Start), DiagnosticsClient.ValidateResponseOptions.UnknownCommandReturnsFalse))
                    {
                        return CreateSessionFromResponse(endpoint, ref response);
                    }
                }

                requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing2);
                response = IpcClient.SendMessageGetContinuation(endpoint, requestMessage);
                DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(Start));

                return CreateSessionFromResponse(endpoint, ref response);
            }
            finally
            {
                response?.Dispose();
            }
        }

        internal static async Task<EventPipeSession> StartAsync(IpcEndpoint endpoint, EventPipeSessionConfiguration config, CancellationToken cancellationToken)
        {
            IpcResponse? response = null;
            try
            {
                IpcMessage requestMessage;

                // Only attempt v4 command if custom rundown keywords are provided.
                if (config.AdditionalRundownKeywords != 0)
                {
                    requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing4);
                    response = await IpcClient.SendMessageGetContinuationAsync(endpoint, requestMessage, cancellationToken).ConfigureAwait(false);

                    if (DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(StartAsync), DiagnosticsClient.ValidateResponseOptions.UnknownCommandReturnsFalse))
                    {
                        return CreateSessionFromResponse(endpoint, ref response);
                    }
                }

                // Only attempt v3 command if stack walk is not requested
                if (!config.RequestStackwalk)
                {
                    requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing3);
                    response = await IpcClient.SendMessageGetContinuationAsync(endpoint, requestMessage, cancellationToken).ConfigureAwait(false);

                    if (DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(StartAsync), DiagnosticsClient.ValidateResponseOptions.UnknownCommandReturnsFalse))
                    {
                        return CreateSessionFromResponse(endpoint, ref response);
                    }
                }

                requestMessage = CreateStartMessage(config, EventPipeCommandId.CollectTracing2);
                response = await IpcClient.SendMessageGetContinuationAsync(endpoint, requestMessage, cancellationToken).ConfigureAwait(false);
                DiagnosticsClient.ValidateResponseMessage(response.Value.Message, nameof(StartAsync));

                return CreateSessionFromResponse(endpoint, ref response);
            }
            finally
            {
                response?.Dispose();
            }
        }

        ///<summary>
        /// Stops the given session
        ///</summary>
        public void Stop()
        {
            if (TryCreateStopMessage(out IpcMessage requestMessage))
            {
                try
                {
                    IpcMessage response = IpcClient.SendMessage(_endpoint, requestMessage);

                    DiagnosticsClient.ValidateResponseMessage(response, nameof(Stop));
                }
                // On non-abrupt exits (i.e. the target process has already exited and pipe is gone, sending Stop command will fail).
                catch (IOException)
                {
                    throw new ServerNotAvailableException("Could not send Stop command. The target process may have exited.");
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (TryCreateStopMessage(out IpcMessage requestMessage))
            {
                try
                {
                    IpcMessage response = await IpcClient.SendMessageAsync(_endpoint, requestMessage, cancellationToken).ConfigureAwait(false);

                    DiagnosticsClient.ValidateResponseMessage(response, nameof(StopAsync));
                }
                // On non-abrupt exits (i.e. the target process has already exited and pipe is gone, sending Stop command will fail).
                catch (IOException)
                {
                    throw new ServerNotAvailableException("Could not send Stop command. The target process may have exited.");
                }
            }
        }

        private static IpcMessage CreateStartMessage(EventPipeSessionConfiguration config, EventPipeCommandId command)
        {
            byte[] payload = command switch
            {
                EventPipeCommandId.CollectTracing => config.SerializeV2(),
                EventPipeCommandId.CollectTracing2 => config.SerializeV2(),
                EventPipeCommandId.CollectTracing3 => config.SerializeV3(),
                EventPipeCommandId.CollectTracing4 => config.SerializeV4(),
                _ => throw new ArgumentException(null, nameof(command))
            };
            return new IpcMessage(DiagnosticsServerCommandSet.EventPipe, (byte)command, payload);
        }

        private static EventPipeSession CreateSessionFromResponse(IpcEndpoint endpoint, ref IpcResponse? response)
        {
            try
            {
                ulong sessionId = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(response.Value.Message.Payload, 0, 8));

                EventPipeSession session = new(endpoint, response.Value, sessionId);
                response = null;
                return session;
            }
            finally
            {
                response?.Dispose();
            }
        }

        private bool TryCreateStopMessage(out IpcMessage stopMessage)
        {
            Debug.Assert(_sessionId > 0);

            // Do not issue another Stop command if it has already been issued for this session instance.
            if (_stopped)
            {
                stopMessage = null;
                return false;
            }
            else
            {
                _stopped = true;
            }

            byte[] payload = BitConverter.GetBytes(_sessionId);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(payload);
            }

            stopMessage = new IpcMessage(DiagnosticsServerCommandSet.EventPipe, (byte)EventPipeCommandId.StopTracing, payload);

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            // If session being disposed hasn't been stopped, attempt to stop it first
            if (!_stopped)
            {
                try
                {
                    Stop();
                }
                catch { } // swallow any exceptions that may be thrown from Stop.
            }

            if (!_disposedValue)
            {
                if (disposing)
                {
                    _response.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
