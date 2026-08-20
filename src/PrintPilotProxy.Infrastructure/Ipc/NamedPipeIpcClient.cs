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
    private const int ConnectTimeoutMs = 3000;
    private const int IoTimeoutMs = 5000;

    private readonly ILogger<NamedPipeIpcClient> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

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
            await stream.ConnectAsync(ConnectTimeoutMs, cancellationToken);

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
        // Acquire lock with a reasonable timeout so concurrent polling requests
        // queue cleanly instead of immediately failing
        bool acquired = await _lock.WaitAsync(3000, cancellationToken);
        if (!acquired)
        {
            throw new TimeoutException("Another IPC request is already in progress.");
        }

        try
        {
            return await SendWithRetryAsync(message, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IpcMessage> SendWithRetryAsync(IpcMessage message, CancellationToken cancellationToken)
    {
        // First attempt: use existing connection if available
        try
        {
            return await SendCoreAsync(message, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                    ex is IOException or TimeoutException or OperationCanceledException)
        {
            _logger.LogDebug(ex, "IPC request failed; will disconnect and retry once.");
            Disconnect();
        }

        // Second attempt: fresh connection, fresh timeout
        return await SendCoreAsync(message, cancellationToken);
    }

    private async Task<IpcMessage> SendCoreAsync(IpcMessage message, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(IoTimeoutMs);
        var ct = cts.Token;

        try
        {
            if (!IsConnected && !await ConnectAsync(ct))
            {
                throw new IOException("Could not connect to the PrintPilotProxy service.");
            }

            var json = JsonSerializer.Serialize(message);
            await _writer!.WriteLineAsync(json.AsMemory(), ct);
            var responseJson = await _reader!.ReadLineAsync(ct);
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
    }

    private void Disconnect()
    {
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _clientStream?.Dispose(); } catch { }
        _reader = null;
        _writer = null;
        _clientStream = null;
    }

    public void Dispose()
    {
        Disconnect();
        _lock.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
