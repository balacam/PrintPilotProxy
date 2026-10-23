using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Constants;
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
    private readonly INetworkInterfaceDiscovery _networkDiscovery;
    private readonly IProxyAuthenticator? _authenticator;
    private readonly IDataProtector _dataProtector;
    private ProxyServer? _proxyServer;
    private readonly List<ExplicitProxyEndPoint> _explicitEndPoints = new();
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
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly BoundedConnectionHistory _seenConnections = new();

    public event EventHandler<ProxyRequestEntry>? RequestProcessed;
    public event EventHandler<ProxyErrorEventArgs>? ErrorOccurred;

    private string GetConnectionId(Guid twpConnectionId) 
    {
        return $"PPP-{DateTime.UtcNow:yyyyMMdd}-{twpConnectionId.ToString("N").Substring(0, 8)}";
    }

    private void EnsureClientConnectionLogged(SessionEventArgsBase e, string connectionId)
    {
        if (_seenConnections.TryAdd(e.ClientConnectionId))
        {
            var endpoint = e.ClientRemoteEndPoint as IPEndPoint;
            _logger.LogInformation("[DIAG] ClientConnectionAccepted ConnectionId={ConnectionId} ClientIP={ClientIP} ClientPort={ClientPort}", 
                connectionId, endpoint?.Address, endpoint?.Port);
        }
    }

    public UnobtaniumProxyEngine(
        ILogger<UnobtaniumProxyEngine> logger,
        IAccessControlList acl,
        INetworkInterfaceDiscovery networkDiscovery,
        IDataProtector dataProtector,
        IProxyAuthenticator? authenticator = null)
    {
        _logger = logger;
        _acl = acl;
        _networkDiscovery = networkDiscovery;
        _dataProtector = dataProtector;
        _authenticator = authenticator;
    }

    public async Task StartAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_state is ProxyState.Running or ProxyState.Starting)
            {
                _logger.LogInformation("UnobtaniumProxyEngine is already running.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (configuration.Security.RequireAuthentication && _authenticator is null)
                throw new InvalidOperationException("Proxy authentication is enabled but no authenticator is registered.");
            _configuration = configuration;
            _state = ProxyState.Starting;
            ResetRunStatistics();
            _proxyServer = new ProxyServer(userTrustRootCertificate: false);

            // The configured idle timeout is an engine setting; without this the engine's own default applied
            // whatever the operator entered.
            _proxyServer.ConnectionTimeOutSeconds = configuration.Listener.ConnectionTimeoutSeconds;
            
            // Generate and store certificate in ProgramData instead of Program Files
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PrintPilotProxy");
            if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
            _proxyServer.CertificateManager.RootCertificateName = "PrintPilotProxyRoot";
            _proxyServer.CertificateManager.PfxFilePath = Path.Combine(dataFolder, "rootCert.pfx");
            _proxyServer.ExceptionFunc = async (exception) =>
            {
                Interlocked.Increment(ref _totalErrors);
                _lastFailedRequest = DateTimeOffset.UtcNow;
                ErrorOccurred?.Invoke(this, new ProxyErrorEventArgs(exception, "ProxyServer Exception"));
            };

            if (configuration.UpstreamProxy.Mode == UpstreamProxyMode.Manual && !string.IsNullOrWhiteSpace(configuration.UpstreamProxy.Host))
            {
                var upstreamPassword = _dataProtector.Unprotect(configuration.UpstreamProxy.ProtectedPassword ?? string.Empty);
                var externalProxy = new ExternalProxy
                {
                    HostName = configuration.UpstreamProxy.Host,
                    Port = configuration.UpstreamProxy.Port,
                    UserName = configuration.UpstreamProxy.Username,
                    Password = upstreamPassword,
                    ProxyType = ExternalProxyType.Http
                };
                _proxyServer.UpStreamHttpProxy = externalProxy;
                _proxyServer.UpStreamHttpsProxy = externalProxy;
            }

            _proxyServer.BeforeRequest += OnBeforeRequest;
            _proxyServer.BeforeResponse += OnBeforeResponse;
            _explicitEndPoints.Clear();

            _logger.LogInformation("[DIAG] ProxyStartStarted Mode={ListenerMode} ConfiguredAddress={ListenAddress} Port={Port}",
                configuration.Listener.Mode, configuration.Listener.ListenAddress, configuration.Listener.Port);

            if (configuration.Listener.Mode == ListenerMode.Auto)
            {
                var ipsEnumerable = await _networkDiscovery.GetUsablePrivateIpAddressesAsync();
                var ips = ipsEnumerable?.ToList() ?? new List<IPAddress>();
                
                if (ips.Count == 0)
                {
                    throw new InvalidOperationException("Automatic listener mode could not find a usable local network address.");
                }
                
                foreach (var ip in ips)
                {
                    AddEndPoint(ip, configuration.Listener.Port);
                }
            }
            else if (configuration.Listener.Mode == ListenerMode.SpecificAddress)
            {
                if (!IPAddress.TryParse(configuration.Listener.ListenAddress, out var ipAddress))
                {
                    throw new InvalidOperationException("The configured listener address is not a valid IP address.");
                }

                if (ipAddress.Equals(IPAddress.Any) || ipAddress.Equals(IPAddress.IPv6Any))
                {
                    throw new InvalidOperationException("Use AllInterfaces mode to bind to every address.");
                }

                var interfaces = await _networkDiscovery.GetInterfacesAsync();
                var isAssigned = IPAddress.IsLoopback(ipAddress) || interfaces
                    .SelectMany(networkInterface => networkInterface.Addresses)
                    .Select(addrStr => IPAddress.TryParse(addrStr, out var parsed) ? parsed : null)
                    .Any(localIp => localIp != null && localIp.Equals(ipAddress));

                if (!isAssigned)
                {
                    throw new InvalidOperationException("The configured listener address is not assigned to this computer.");
                }

                AddEndPoint(ipAddress, configuration.Listener.Port);
            }
            else if (configuration.Listener.Mode == ListenerMode.SpecificAdapter)
            {
                if (string.IsNullOrWhiteSpace(configuration.Listener.AdapterName))
                {
                    throw new InvalidOperationException("A network adapter must be selected for SpecificAdapter mode.");
                }

                var selectedAdapter = (await _networkDiscovery.GetInterfacesAsync())
                    .FirstOrDefault(networkInterface => string.Equals(
                        networkInterface.Name,
                        configuration.Listener.AdapterName,
                        StringComparison.OrdinalIgnoreCase));
                // Every usable address on the adapter is bound, as in Auto mode. Binding only the first one could leave
                // the proxy on an IPv6 address alone, because Windows often lists IPv6 first, and IPv4 clients would
                // then be unable to connect.
                var selectedAddresses = (selectedAdapter?.Addresses ?? new List<string>())
                    .Select(addrStr => IPAddress.TryParse(addrStr, out var parsed) ? parsed : null)
                    .Where(ip => ip != null)
                    .Select(ip => ip!)
                    .Distinct()
                    .ToList();
                if (selectedAddresses.Count == 0)
                {
                    throw new InvalidOperationException("The selected network adapter has no usable assigned IP address.");
                }

                foreach (var selectedAddress in selectedAddresses)
                {
                    AddEndPoint(selectedAddress, configuration.Listener.Port);
                }
            }
            else if (configuration.Listener.Mode == ListenerMode.AllInterfaces)
            {
                AddEndPoint(IPAddress.Any, configuration.Listener.Port);
            }

            WarnAboutExposure(configuration);

#pragma warning disable CS0618
            _proxyServer.Start(false); // Starting proxy server
#pragma warning restore CS0618
            
            _state = ProxyState.Running;
            _startedAt = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("[DIAG] ProxyStartSucceeded ListeningOn=[{ListeningEndpoints}]",
                string.Join(", ", _explicitEndPoints.Select(e => $"{e.IpAddress}:{e.Port}")));
            _logger.LogInformation("UnobtaniumProxyEngine started on {ListenerMode} mode on port {Port}", configuration.Listener.Mode, configuration.Listener.Port);
        }
        catch (Exception ex)
        {
            _state = ProxyState.Faulted;
            try
            {
                DisposeServer();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error during proxy server cleanup after start failure.");
            }

            _logger.LogError(ex, "[DIAG] ProxyStartFailed ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage}",
                ex.GetType().Name, ex.Message);
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
            if (_state == ProxyState.Stopped && _proxyServer is null)
            {
                return;
            }

            _state = ProxyState.Stopping;
            DisposeServer();
            _state = ProxyState.Stopped;
            _logger.LogInformation("UnobtaniumProxyEngine stopped.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public ProxyStatus GetStatus()
    {
        var currentState = _state;
        string? listeningAddress;
        lock (_explicitEndPoints)
        {
            listeningAddress = _explicitEndPoints.Any() ? string.Join(", ", _explicitEndPoints.Select(e => $"{e.IpAddress}:{e.Port}")) : null;
        }
        
        return new ProxyStatus
        {
            State = currentState,
            ListeningAddress = listeningAddress,
            StartedAt = _startedAt,
            TotalRequests = Interlocked.Read(ref _totalRequests),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            TotalBytesTransferred = Interlocked.Read(ref _totalBytesTransferred),
            LastSuccessfulRequest = _lastSuccessfulRequest,
            LastFailedRequest = _lastFailedRequest,
            ActiveConnections = _proxyServer?.ClientConnectionCount ?? 0,
            EngineName = "Unobtanium Web Proxy",
            EngineVersion = typeof(ProxyServer).Assembly.GetName().Version?.ToString(3) ?? "unknown"
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
        _lifecycleLock.Dispose();
    }

    private Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
    {
        e.DecryptSsl = false;
        
        var connectionId = GetConnectionId(e.ClientConnectionId);
        EnsureClientConnectionLogged(e, connectionId);

        var targetHost = e.HttpClient.Request.RequestUri?.Host ?? "unknown";
        var targetPort = e.HttpClient.Request.RequestUri?.Port ?? 0;
        
        _logger.LogInformation("[DIAG] CONNECT_RECEIVED ConnectionId={ConnectionId} TargetHost={TargetHost} TargetPort={TargetPort}",
            connectionId, targetHost, targetPort);

        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;

        if (EvaluateAccess(e, clientIp, connectionId) is { } denial)
        {
            if (denial.StatusCode == 407)
            {
                e.HttpClient.Response.HttpVersion = e.HttpClient.Request.HttpVersion;
                e.HttpClient.Response.StatusCode = 407;
                e.HttpClient.Response.StatusDescription = "Proxy Authentication Required";
                e.HttpClient.Response.Headers.AddHeader("Proxy-Authenticate", AuthenticateChallenge);
                e.HttpClient.Response.ContentLength = 0;
            }

            e.DenyConnect = true;
            LogRequest(e, clientIp, denial.StatusCode, denial.LogReason);
            return Task.CompletedTask;
        }

        var externalProxy = e.CustomUpStreamProxy ?? _proxyServer?.UpStreamHttpsProxy;
        if (externalProxy != null)
        {
            _logger.LogInformation("[DIAG] UpstreamConnectionStarted ConnectionId={ConnectionId} UpstreamHost={UpstreamHost} UpstreamPort={UpstreamPort} TargetHost={TargetHost} TargetPort={TargetPort}",
                connectionId, externalProxy.HostName, externalProxy.Port, targetHost, targetPort);
        }
        else
        {
            _logger.LogInformation("[DIAG] OutboundConnectionStarted ConnectionId={ConnectionId} TargetHost={TargetHost} TargetPort={TargetPort} Mode=Direct",
                connectionId, targetHost, targetPort);
        }

        // We do not log 200 here anymore, because the target tunnel hasn't been established yet.
        // We defer logging to OnBeforeTunnelConnectResponse.
        return Task.CompletedTask;
    }

    private Task OnBeforeTunnelConnectResponse(object sender, TunnelConnectSessionEventArgs e)
    {
        var connectionId = GetConnectionId(e.ClientConnectionId);
        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;
        
        // If DenyConnect is true, we already logged it in Request.
        if (e.DenyConnect) return Task.CompletedTask;

        var statusCode = e.HttpClient.Response.StatusCode;
        _logger.LogInformation("[DIAG] ConnectResponseSent ConnectionId={ConnectionId} StatusCode={StatusCode}", connectionId, statusCode);

        if (statusCode >= 200 && statusCode < 300)
        {
            var targetHost = e.HttpClient.Request.RequestUri?.Host ?? "unknown";
            var targetPort = e.HttpClient.Request.RequestUri?.Port ?? 0;
            
            _logger.LogInformation("[DIAG] UpstreamConnectionEstablished ConnectionId={ConnectionId}", connectionId);
            _logger.LogInformation("[DIAG] TunnelEstablished ConnectionId={ConnectionId} TargetHost={TargetHost} TargetPort={TargetPort}", 
                connectionId, targetHost, targetPort);
            
            
            LogRequest(e, clientIp, statusCode, null);
        }
        else
        {
            var errorType = e.Exception?.GetType().Name ?? "Unknown";
            var errorMsg = e.Exception?.Message ?? $"Status code: {statusCode}";
            
            _logger.LogInformation("[DIAG] UpstreamConnectionFailed ConnectionId={ConnectionId} ErrorCategory={ErrorCategory} ExceptionType={ExceptionType} ErrorMessage={ErrorMessage}", 
                connectionId, "TunnelError", errorType, errorMsg);
            
            _logger.LogInformation("[DIAG] TunnelFailed ConnectionId={ConnectionId} Reason={Reason}", connectionId, $"Failed to establish tunnel. Status: {statusCode}");
            
            LogRequest(e, clientIp, statusCode, $"Tunnel failed with status: {statusCode}");
        }

        return Task.CompletedTask;
    }

    private Task OnBeforeRequest(object sender, SessionEventArgs e)
    {
        var connectionId = GetConnectionId(e.ClientConnectionId);
        EnsureClientConnectionLogged(e, connectionId);

        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;

        if (EvaluateAccess(e, clientIp, connectionId) is { } denial)
        {
            var headers = denial.StatusCode == 407
                ? new List<HttpHeader> { new("Proxy-Authenticate", AuthenticateChallenge) }
                : new List<HttpHeader>();
            e.GenericResponse(denial.ResponseMessage, (HttpStatusCode)denial.StatusCode, headers);
            LogRequest(e, clientIp, denial.StatusCode, denial.LogReason);
            return Task.CompletedTask;
        }

        // The outcome is only known once the upstream answers, so it is recorded in OnBeforeResponse.
        e.UserData = AllowedRequestMarker;
        return Task.CompletedTask;
    }

    private const string AllowedRequestMarker = "printpilot-allowed";

    private Task OnBeforeResponse(object sender, SessionEventArgs e)
    {
        // Requests rejected locally were already logged with their own status; only allowed ones are completed here.
        if (!Equals(e.UserData, AllowedRequestMarker))
        {
            return Task.CompletedTask;
        }

        e.UserData = null;
        var clientIp = (e.ClientRemoteEndPoint as IPEndPoint)?.Address;
        var status = e.HttpClient.Response.StatusCode;
        var length = e.HttpClient.Response.ContentLength;
        LogRequest(e, clientIp, status, status >= 400 ? $"Upstream responded with status {status}" : null, length > 0 ? length : 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tells the operator when the proxy is open to the internet or to every client on the network. Nothing is blocked;
    /// existing setups keep working, but the risk is visible in the log.
    /// </summary>
    private void WarnAboutExposure(ProxyConfiguration configuration)
    {
        List<IPAddress> addresses;
        lock (_explicitEndPoints)
        {
            addresses = _explicitEndPoints.Select(e => e.IpAddress).ToList();
        }

        foreach (var address in addresses.Where(PrintPilotProxy.Core.Validation.NetworkAddressClassifier.IsPubliclyRoutable))
        {
            _logger.LogWarning(
                "The proxy is listening on {Address}, a publicly routable address. Anyone who can reach it from the internet can try to use it. " +
                "Bind a specific private address or put a firewall in front of it.", address);
        }

        var open = configuration.ClientAccess.Mode == ClientAccessMode.AllowAll && !configuration.Security.RequireAuthentication;
        if (open && addresses.Any(a => !IPAddress.IsLoopback(a)))
        {
            _logger.LogWarning(
                "Client access is AllowAll with no authentication: every client that can reach the proxy may use it. " +
                "Switch ClientAccess.Mode to AllowList or enable authentication.");
        }
    }

    private long _lastLimitWarningTicks;

    /// <summary>
    /// Reports when more client connections are open than <c>Listener.MaxConnections</c>. This is deliberately a
    /// warning and not a refusal: the engine's connection count includes half-open connections and ones that only
    /// disappear after the idle timeout, so refusing on it would let any LAN host lock every client out by opening
    /// connections without finishing a request. The idle timeout (<c>ConnectionTimeoutSeconds</c>) is enforced.
    /// </summary>
    private void WarnIfOverConnectionLimit()
    {
        var current = _proxyServer?.ClientConnectionCount ?? 0;
        var max = _configuration?.Listener.MaxConnections ?? int.MaxValue;
        if (!ConnectionLimit.IsExceeded(current, max)) return;

        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastLimitWarningTicks);
        if (now - last < TimeSpan.FromMinutes(1).Ticks) return;
        if (Interlocked.CompareExchange(ref _lastLimitWarningTicks, now, last) != last) return;

        _logger.LogWarning(
            "{Current} client connections are open (configured maximum {Max}). The limit is advisory: connections are not refused.",
            current, max);
    }

    private static readonly string AuthenticateChallenge = $"{DiscoveryConstants.AuthScheme} realm=\"PrintPilotProxy\"";

    /// <summary>Why a request is refused: the status code, the reason recorded in the request log, and the body text.</summary>
    private sealed record AccessDenial(int StatusCode, string LogReason, string ResponseMessage);

    /// <summary>
    /// Applies the deny-by-default checks shared by CONNECT tunnels and plain HTTP requests, in order: client ACL,
    /// proxy authentication, then destination port. Returns <c>null</c> when the request may proceed.
    /// </summary>
    private AccessDenial? EvaluateAccess(SessionEventArgsBase e, IPAddress? clientIp, string connectionId)
    {
        var isAllowed = clientIp != null
            ? _acl.IsAllowed(clientIp)
            : _configuration?.ClientAccess.Mode == ClientAccessMode.AllowAll;

        if (!isAllowed)
        {
            return new AccessDenial(403, "Access denied by ACL", "Access denied by ACL");
        }

        WarnIfOverConnectionLimit();

        if (_authenticator != null && _configuration?.Security.RequireAuthentication == true)
        {
            _logger.LogInformation("[DIAG] AuthenticationStarted ConnectionId={ConnectionId}", connectionId);
            var authHeader = GetHeaderValue(e.HttpClient.Request.Headers, "Proxy-Authorization")
                ?? GetHeaderValue(e.HttpClient.Request.Headers, "X-PrintPilot-Auth");
            var authResult = _authenticator.Authenticate(authHeader, clientIp ?? IPAddress.Any);
            if (!authResult.IsSuccess)
            {
                _logger.LogInformation("[DIAG] AuthenticationFailed ConnectionId={ConnectionId} Reason={Reason}", connectionId, authResult.FailureReason);
                return new AccessDenial(407, $"Proxy authentication required: {authResult.FailureReason}", "Proxy Authentication Required");
            }
            _logger.LogInformation("[DIAG] AuthenticationSucceeded ConnectionId={ConnectionId}", connectionId);
        }

        var destinationUri = e.HttpClient.Request.RequestUri;
        if (destinationUri is null)
        {
            return new AccessDenial(400, "Destination URI is missing", "Destination URI is required");
        }

        var destPort = destinationUri.Port;
        var isDestAllowed = _acl.IsDestinationPortAllowed(destPort);
        _logger.LogInformation("[DIAG] DestinationAclCheck ConnectionId={ConnectionId} TargetHost={TargetHost} TargetPort={TargetPort} Result={Result}",
            connectionId, destinationUri.Host, destPort, isDestAllowed ? "Allowed" : "Denied");

        return isDestAllowed
            ? null
            : new AccessDenial(403, $"Destination port {destPort} is not allowed", $"Destination port {destPort} is not allowed");
    }

    private void AddEndPoint(IPAddress address, int port)
    {
        var endpoint = new ExplicitProxyEndPoint(address, port, decryptSsl: false);
        endpoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;
        endpoint.BeforeTunnelConnectResponse += OnBeforeTunnelConnectResponse;
        _proxyServer!.AddEndPoint(endpoint);
        _explicitEndPoints.Add(endpoint);
    }

    private void LogRequest(SessionEventArgsBase e, IPAddress? clientIp, int statusCode, string? error, long bytesTransferred = 0)
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
            Destination = PrintPilotProxy.Core.Validation.RequestUriMasker.Mask(e.HttpClient.Request.RequestUri),
            StatusCode = statusCode,
            BytesTransferred = bytesTransferred,
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

    private void DisposeServer()
    {
        if (_proxyServer is null)
        {
            return;
        }

        try
        {
            _proxyServer.BeforeRequest -= OnBeforeRequest;
            _proxyServer.BeforeResponse -= OnBeforeResponse;
            foreach (var endpoint in _explicitEndPoints)
            {
                endpoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
                endpoint.BeforeTunnelConnectResponse -= OnBeforeTunnelConnectResponse;
                _proxyServer.RemoveEndPoint(endpoint);
            }

            _explicitEndPoints.Clear();

            try
            {
                _proxyServer.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ProxyServer.Stop() encountered an exception during cleanup (ignorable if server was not running).");
            }

            try
            {
                _proxyServer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ProxyServer.Dispose() encountered an exception during cleanup.");
            }
        }
        finally
        {
            _proxyServer = null;
        }
    }

    private void ResetRunStatistics()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _totalBytesTransferred, 0);
        _lastSuccessfulRequest = null;
        _lastFailedRequest = null;
        _startedAt = null;
        _recentRequests.Clear();
        _seenConnections.Clear();
    }

    private static string? GetHeaderValue(HeaderCollection? headers, string headerName)
    {
        if (headers == null) return null;
        return headers.FirstOrDefault(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
