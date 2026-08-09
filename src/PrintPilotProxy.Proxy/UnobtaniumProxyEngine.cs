using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace PrintPilotProxy.Proxy;

/// <summary>
/// Implementation of IProxyEngine using Unobtanium.Web.Proxy.
/// </summary>
public sealed class UnobtaniumProxyEngine : IProxyEngine
{
    private readonly ILogger<UnobtaniumProxyEngine> _logger;
    private readonly IAccessControlList _acl;
    private ProxyServer? _proxyServer;
    private ExplicitProxyEndPoint? _explicitEndPoint;
    private ProxyConfiguration? _configuration;

    private ProxyState _state = ProxyState.Stopped;
    private DateTimeOffset? _startedAt;
    
    private long _totalRequests;
    private long _totalErrors;
    private long _totalBytesTransferred;
    
    private DateTimeOffset? _lastSuccessfulRequest;
    private DateTimeOffset? _lastFailedRequest;

    private readonly ConcurrentQueue<ProxyRequestEntry> _recentRequests = new();
    private const int MaxRecentRequests = 1000;

    public event EventHandler<ProxyRequestEntry>? RequestProcessed;
    public event EventHandler<ProxyErrorEventArgs>? ErrorOccurred;

    public UnobtaniumProxyEngine(ILogger<UnobtaniumProxyEngine> logger, IAccessControlList acl)
    {
        _logger = logger;
        _acl = acl;
    }

    public async Task StartAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _configuration = configuration;
        _state = ProxyState.Starting;

        try
        {
            _proxyServer = new ProxyServer(userTrustRootCertificate: false);
            
            _proxyServer.ExceptionFunc = async (exception) =>
            {
                Interlocked.Increment(ref _totalErrors);
                _lastFailedRequest = DateTimeOffset.UtcNow;
                ErrorOccurred?.Invoke(this, new ProxyErrorEventArgs(exception, "ProxyServer Exception"));
            };

            _proxyServer.BeforeRequest += OnBeforeRequest;

            if (!IPAddress.TryParse(configuration.Listener.ListenAddress, out var ipAddress))
            {
                ipAddress = IPAddress.Any;
            }

            _explicitEndPoint = new ExplicitProxyEndPoint(ipAddress, configuration.Listener.Port, decryptSsl: false);
            _explicitEndPoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;

            _proxyServer.AddEndPoint(_explicitEndPoint);
#pragma warning disable CS0618
            _proxyServer.Start(false); // Starting proxy server
#pragma warning restore CS0618
            
            _state = ProxyState.Running;
            _startedAt = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("UnobtaniumProxyEngine started on {IpAddress}:{Port}", ipAddress, configuration.Listener.Port);
        }
        catch (Exception ex)
        {
            _state = ProxyState.Faulted;
            _logger.LogError(ex, "Failed to start UnobtaniumProxyEngine.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _state = ProxyState.Stopping;

        if (_proxyServer != null)
        {
            _proxyServer.BeforeRequest -= OnBeforeRequest;
            
            if (_explicitEndPoint != null)
            {
                _explicitEndPoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
                _proxyServer.RemoveEndPoint(_explicitEndPoint);
                _explicitEndPoint = null;
            }

            _proxyServer.Stop();
            _proxyServer.Dispose();
            _proxyServer = null;
        }

        _state = ProxyState.Stopped;
        _logger.LogInformation("UnobtaniumProxyEngine stopped.");

        return Task.CompletedTask;
    }

    public ProxyStatus GetStatus()
    {
        return new ProxyStatus
        {
            State = _state,
            ListeningAddress = _explicitEndPoint != null ? $"{_explicitEndPoint.IpAddress}:{_explicitEndPoint.Port}" : null,
            StartedAt = _startedAt,
            TotalRequests = Interlocked.Read(ref _totalRequests),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            TotalBytesTransferred = Interlocked.Read(ref _totalBytesTransferred),
            LastSuccessfulRequest = _lastSuccessfulRequest,
            LastFailedRequest = _lastFailedRequest,
            ActiveConnections = _proxyServer?.ClientConnectionCount ?? 0,
            EngineName = "Unobtanium Web Proxy",
            EngineVersion = "0.1.5"
        };
    }

    public IReadOnlyList<ProxyRequestEntry> GetRecentRequests(int count = 100)
    {
        var entries = _recentRequests.ToArray();
        return entries.Skip(Math.Max(0, entries.Length - count)).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
    {
        e.DecryptSsl = false;
        
        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;

        if (clientIp == null || !_acl.IsAllowed(clientIp))
        {
            e.DenyConnect = true;
            LogRequest(e, clientIp, 403, "Access denied by ACL");
            return Task.CompletedTask;
        }
        
        int destPort = e.HttpClient.Request.RequestUri.Port;
        if (!_acl.IsDestinationPortAllowed(destPort))
        {
            e.DenyConnect = true;
            LogRequest(e, clientIp, 403, $"Destination port {destPort} is not allowed");
            return Task.CompletedTask;
        }

        LogRequest(e, clientIp, 200, null);
        return Task.CompletedTask;
    }

    private Task OnBeforeRequest(object sender, SessionEventArgs e)
    {
        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;
        
        if (clientIp == null || !_acl.IsAllowed(clientIp))
        {
            RejectRequest(e, HttpStatusCode.Forbidden, "Access denied by ACL");
            LogRequest(e, clientIp, 403, "Access denied by ACL");
            return Task.CompletedTask;
        }
        
        int destPort = e.HttpClient.Request.RequestUri.Port;
        if (!_acl.IsDestinationPortAllowed(destPort))
        {
            RejectRequest(e, HttpStatusCode.Forbidden, $"Destination port {destPort} is not allowed");
            LogRequest(e, clientIp, 403, $"Destination port {destPort} is not allowed");
            return Task.CompletedTask;
        }

        LogRequest(e, clientIp, 200, null);
        return Task.CompletedTask;
    }
    
    private void RejectRequest(SessionEventArgs e, HttpStatusCode status, string message)
    {
        e.GenericResponse(message, status, new List<HttpHeader>());
    }

    private void LogRequest(SessionEventArgsBase e, IPAddress? clientIp, int statusCode, string? error)
    {
        Interlocked.Increment(ref _totalRequests);
        
        if (statusCode >= 400 || !string.IsNullOrEmpty(error))
        {
            Interlocked.Increment(ref _totalErrors);
            _lastFailedRequest = DateTimeOffset.UtcNow;
        }
        else
        {
            _lastSuccessfulRequest = DateTimeOffset.UtcNow;
        }

        var entry = new ProxyRequestEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            ClientIp = clientIp?.ToString() ?? "Unknown",
            Method = e.HttpClient.Request.Method ?? "UNKNOWN",
            Destination = e.HttpClient.Request.RequestUri?.ToString() ?? "Unknown",
            StatusCode = statusCode,
            BytesTransferred = 0,
            ErrorMessage = error
        };

        Interlocked.Add(ref _totalBytesTransferred, entry.BytesTransferred);

        _recentRequests.Enqueue(entry);
        while (_recentRequests.Count > MaxRecentRequests)
        {
            _recentRequests.TryDequeue(out _);
        }

        RequestProcessed?.Invoke(this, entry);
    }
}
