using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Ipc
{
    public class NamedPipeIpcClient : IIpcClient, IDisposable, IAsyncDisposable
    {
        private readonly ILogger<NamedPipeIpcClient> _logger;
        private const string PipeName = "PrintPilotProxy";
        private NamedPipeClientStream? _clientStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        public bool IsConnected => _clientStream?.IsConnected ?? false;

        public NamedPipeIpcClient(ILogger<NamedPipeIpcClient> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected) return true;

            try
            {
                _clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _clientStream.ConnectAsync(5000, cancellationToken);
                
                _reader = new StreamReader(_clientStream);
                _writer = new StreamWriter(_clientStream) { AutoFlush = true };
                
                _logger.LogInformation("Connected to IPC server.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to IPC server.");
                return false;
            }
        }

        public async Task<IpcMessage> SendAsync(IpcMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _writer == null || _reader == null)
            {
                await ConnectAsync(cancellationToken);
            }

            try
            {
                string json = JsonSerializer.Serialize(message);
                await _writer!.WriteLineAsync(json.AsMemory(), cancellationToken);

                string? responseJson = await _reader!.ReadLineAsync();
                if (string.IsNullOrEmpty(responseJson))
                {
                    throw new IOException("Server closed the connection.");
                }

                var response = JsonSerializer.Deserialize<IpcMessage>(responseJson);
                return response ?? new IpcMessage { Type = IpcMessageTypes.Error, Payload = "Empty response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send IPC message.");
                _clientStream?.Dispose();
                _clientStream = null;
                throw;
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _clientStream?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_writer != null) await _writer.DisposeAsync();
            if (_clientStream != null) await _clientStream.DisposeAsync();
            _reader?.Dispose();
        }
    }
}
