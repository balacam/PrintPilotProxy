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
    public class NamedPipeIpcServer : IIpcServer, IAsyncDisposable
    {
        private readonly ILogger<NamedPipeIpcServer> _logger;
        private const string PipeName = "PrintPilotProxy";
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public event Func<IpcMessage, Task<IpcMessage>>? MessageReceived;

        public NamedPipeIpcServer(ILogger<NamedPipeIpcServer> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_cts != null)
                return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token), CancellationToken.None);
            
            _logger.LogInformation("IPC Server started on pipe {PipeName}", PipeName);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_serverTask != null)
                {
                    try
                    {
                        await _serverTask.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping IPC server.");
                    }
                }
                _cts.Dispose();
                _cts = null;
                _logger.LogInformation("IPC Server stopped.");
            }
        }

        private async Task ServerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var serverStream = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await serverStream.WaitForConnectionAsync(token);
                    
                    // Handle connection
                    _ = HandleConnectionAsync(serverStream, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in IPC server loop.");
                    await Task.Delay(1000, token); // Backoff on error
                }
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream stream, CancellationToken token)
        {
            try
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

                while (stream.IsConnected && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        break;

                    var message = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (message != null)
                    {
                        IpcMessage response;
                        if (MessageReceived != null)
                        {
                            response = await MessageReceived(message);
                        }
                        else
                        {
                            response = new IpcMessage 
                            { 
                                Type = IpcMessageTypes.Success,
                                Payload = "Received (No handler)"
                            };
                        }
                        string responseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling IPC connection.");
            }
            finally
            {
                if (stream.IsConnected)
                    stream.Disconnect();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
