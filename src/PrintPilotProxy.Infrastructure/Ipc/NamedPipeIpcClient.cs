using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>Thread-safe client for the local PrintPilotProxy management pipe.</summary>
public sealed class NamedPipeIpcClient : IIpcClient, IDisposable, IAsyncDisposable
{
    private readonly ILogger<NamedPipeIpcClient> _logger;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private NamedPipeClientStream? _clientStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _clientStream?.IsConnected == true;

    public NamedPipeIpcClient(ILogger<NamedPipeIpcClient>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NamedPipeIpcClient>.Instance;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        Disconnect();
        try
        {
            var stream = new NamedPipeClientStream(
                ".", NamedPipeIpcServer.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            await stream.ConnectAsync(5000, cancellationToken);

            _clientStream = stream;
            _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096, leaveOpen: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
            _logger.LogInformation("Connected to local IPC server.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not connect to local IPC server.");
            Disconnect();
            return false;
        }
    }

    public async Task<IpcMessage> SendAsync(IpcMessage message, CancellationToken cancellationToken = default)
    {
        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsConnected && !await ConnectAsync(cancellationToken))
            {
                throw new IOException("Could not connect to the PrintPilotProxy service.");
            }

            var json = JsonSerializer.Serialize(message);
            await _writer!.WriteLineAsync(json.AsMemory(), cancellationToken);
            var responseJson = await _reader!.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new IOException("The service closed the IPC connection.");
            }

            var response = JsonSerializer.Deserialize<IpcMessage>(responseJson);
            if (response is null)
            {
                throw new IOException("The service returned an invalid IPC response.");
            }

            return response;
        }
        catch
        {
            Disconnect();
            throw;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _clientStream?.Dispose();
        _reader = null;
        _writer = null;
        _clientStream = null;
    }

    public void Dispose()
    {
        Disconnect();
        _requestLock.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        _requestLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
