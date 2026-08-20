using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Discovery;

/// <summary>
/// UDP broadcast transport for LAN discovery on port 37421.
/// </summary>
public sealed class UdpBroadcastDiscoveryTransport : IProxyDiscoveryTransport
{
    private readonly ILogger<UdpBroadcastDiscoveryTransport> _logger;
    private readonly IDiscoveryRateLimiter _rateLimiter;
    private readonly int _port;
    private UdpClient? _udpClient;
    private Task? _receiveLoopTask;
    private CancellationTokenSource? _cts;
    private Func<DiscoveryRequest, IPEndPoint, Task<DiscoveryResponse?>>? _handler;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private bool _isRunning;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string TransportName => "UDP Broadcast";
    public int Port => _port;
    public bool IsRunning => _isRunning;

    public UdpBroadcastDiscoveryTransport(
        IDiscoveryRateLimiter rateLimiter,
        ILogger<UdpBroadcastDiscoveryTransport>? logger = null,
        int port = DiscoveryConstants.DefaultUdpPort)
    {
        _rateLimiter = rateLimiter;
        _port = port;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UdpBroadcastDiscoveryTransport>.Instance;
    }

    public async Task StartAsync(
        Func<DiscoveryRequest, IPEndPoint, Task<DiscoveryResponse?>> messageHandler,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRunning)
            {
                return;
            }

            _handler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _cts = new CancellationTokenSource();

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));

            _isRunning = true;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

            _logger.LogInformation("PrintPilotProxy UDP discovery transport started on port {Port}.", _port);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            CleanupSocket();
            _logger.LogError(ex, "Failed to start UDP discovery transport on port {Port}.", _port);
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch { }
            }

            CleanupSocket();

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
                }
                catch { }
                _receiveLoopTask = null;
            }

            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }

            _logger.LogInformation("PrintPilotProxy UDP discovery transport stopped.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _isRunning && _udpClient != null)
        {
            try
            {
                var receiveResult = await _udpClient.ReceiveAsync(stoppingToken);
                var clientEndPoint = receiveResult.RemoteEndPoint;

                // Rate limiting per client IP
                if (!_rateLimiter.ShouldAllow(clientEndPoint.Address))
                {
                    _logger.LogDebug("Throttled discovery request from {ClientEndPoint}.", clientEndPoint);
                    continue;
                }

                // Defensive packet length check (ignore excessively large packets)
                if (receiveResult.Buffer.Length == 0 || receiveResult.Buffer.Length > 8192)
                {
                    continue;
                }

                DiscoveryRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<DiscoveryRequest>(receiveResult.Buffer, _jsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Ignored malformed or non-JSON UDP discovery packet from {ClientEndPoint}.", clientEndPoint);
                    continue;
                }

                if (request == null || _handler == null)
                {
                    continue;
                }

                var response = await _handler(request, clientEndPoint);
                if (response != null && _udpClient != null && _isRunning)
                {
                    var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions);
                    await _udpClient.SendAsync(responseBytes, responseBytes.Length, clientEndPoint);
                    _logger.LogDebug("Sent discovery response to {ClientEndPoint} for service {Service}.", clientEndPoint, response.Service);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex) when (stoppingToken.IsCancellationRequested || !_isRunning)
            {
                _logger.LogDebug(ex, "UDP discovery socket closed during shutdown.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing UDP discovery packet.");
            }
        }
    }

    private void CleanupSocket()
    {
        try
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        catch { }
        finally
        {
            _udpClient = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleLock.Dispose();
    }
}
