using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Discovery;

/// <summary>
/// Implements the PrintPilotProxy LAN Discovery Service coordinating transports, dynamic interface routing,
/// instance identity, and versioned discovery responses.
/// </summary>
public sealed class ProxyDiscoveryService : IProxyDiscoveryService
{
    private readonly ILogger<ProxyDiscoveryService> _logger;
    private readonly IProxyDiscoveryTransport _transport;
    private readonly IProxyEngine _proxyEngine;
    private readonly IConfigurationManager _configManager;
    private readonly INetworkInterfaceDiscovery _networkDiscovery;
    private readonly IProxyInstanceProvider _instanceProvider;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private bool _isRunning;
    private long _totalRequestsReceived;
    private long _totalResponsesSent;
    private DateTimeOffset? _lastRequestReceivedAt;
    private string? _lastError;

    public event EventHandler<DiscoveryRequestEventArgs>? RequestReceived;
    public event EventHandler<DiscoveryResponseEventArgs>? ResponseSent;

    public bool IsRunning => _isRunning;

    public ProxyDiscoveryService(
        IProxyDiscoveryTransport transport,
        IProxyEngine proxyEngine,
        IConfigurationManager configManager,
        INetworkInterfaceDiscovery networkDiscovery,
        IProxyInstanceProvider instanceProvider,
        ILogger<ProxyDiscoveryService>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _proxyEngine = proxyEngine ?? throw new ArgumentNullException(nameof(proxyEngine));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _networkDiscovery = networkDiscovery ?? throw new ArgumentNullException(nameof(networkDiscovery));
        _instanceProvider = instanceProvider ?? throw new ArgumentNullException(nameof(instanceProvider));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProxyDiscoveryService>.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRunning)
            {
                return;
            }

            _logger.LogInformation("Starting PrintPilotProxy discovery service...");
            await _transport.StartAsync(HandleDiscoveryRequestAsync, cancellationToken);
            _isRunning = true;
            _logger.LogInformation("PrintPilotProxy discovery service started successfully on transport {TransportName}.", _transport.TransportName);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to start PrintPilotProxy discovery service.");
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

            _logger.LogInformation("Stopping PrintPilotProxy discovery service...");
            await _transport.StopAsync(cancellationToken);
            _isRunning = false;
            _logger.LogInformation("PrintPilotProxy discovery service stopped.");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogWarning(ex, "Error while stopping PrintPilotProxy discovery service.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public DiscoveryStatus GetStatus()
    {
        return new DiscoveryStatus
        {
            IsRunning = _isRunning && _transport.IsRunning,
            Port = _transport.Port,
            TotalRequestsReceived = Interlocked.Read(ref _totalRequestsReceived),
            TotalResponsesSent = Interlocked.Read(ref _totalResponsesSent),
            LastRequestReceivedAt = _lastRequestReceivedAt,
            LastError = _lastError
        };
    }

    public async Task<DiscoveryResponse?> HandleDiscoveryRequestAsync(DiscoveryRequest request, IPEndPoint clientEndPoint)
    {
        try
        {
            Interlocked.Increment(ref _totalRequestsReceived);
            _lastRequestReceivedAt = DateTimeOffset.UtcNow;

            // Validate service name and action
            if (!string.Equals(request.Service, DiscoveryConstants.ServiceName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Ignored discovery request for unknown service '{Service}' from {ClientEndPoint}.", request.Service, clientEndPoint);
                return null;
            }

            if (!string.Equals(request.Request, DiscoveryConstants.DiscoverRequestAction, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Ignored discovery request with unknown action '{Action}' from {ClientEndPoint}.", request.Request, clientEndPoint);
                return null;
            }

            if (request.ProtocolVersion < 1)
            {
                _logger.LogDebug("Ignored discovery request with unsupported protocol version {Version} from {ClientEndPoint}.", request.ProtocolVersion, clientEndPoint);
                return null;
            }

            RequestReceived?.Invoke(this, new DiscoveryRequestEventArgs(request, clientEndPoint));

            // Determine active proxy port
            var proxyPort = await DetermineActiveProxyPortAsync();

            // Determine optimal local host IP matching the client's network interface
            var (hostIp, interfaceName) = await ResolveHostAddressForClientAsync(clientEndPoint.Address);

            var status = _proxyEngine.GetStatus();
            var instanceId = _instanceProvider.GetInstanceId();
            var appVersion = typeof(ProxyDiscoveryService).Assembly.GetName().Version?.ToString(3) ?? "0.5.0";

            var response = new DiscoveryResponse
            {
                Service = DiscoveryConstants.ServiceName,
                ProtocolVersion = DiscoveryConstants.ProtocolVersion,
                Version = appVersion,
                Host = hostIp.ToString(),
                ProxyPort = proxyPort,
                InstanceId = instanceId,
                Protocol = DiscoveryConstants.HttpConnectProtocol,
                AuthProtocol = DiscoveryConstants.AuthScheme,
                InterfaceName = interfaceName,
                Status = status.State.ToString()
            };

            Interlocked.Increment(ref _totalResponsesSent);
            ResponseSent?.Invoke(this, new DiscoveryResponseEventArgs(response, clientEndPoint));

            _logger.LogInformation("Discovery request from {ClientEndPoint} answered with host {Host}:{Port} (instance: {InstanceId}).",
                clientEndPoint, response.Host, response.ProxyPort, response.InstanceId);

            return response;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Error handling discovery request from {ClientEndPoint}.", clientEndPoint);
            return null;
        }
    }

    private async Task<int> DetermineActiveProxyPortAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            if (config.Listener.Port > 0)
            {
                return config.Listener.Port;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read port from configuration manager, using default 3128.");
        }

        return 3128;
    }

    private async Task<(IPAddress HostIp, string? InterfaceName)> ResolveHostAddressForClientAsync(IPAddress clientAddress)
    {
        // 1. If client is loopback, respond with loopback
        if (IPAddress.IsLoopback(clientAddress))
        {
            return (IPAddress.Loopback, "Loopback");
        }

        // 2. Fetch fresh network interfaces to handle dynamic DHCP IP changes without stale caching
        var interfaces = (await _networkDiscovery.GetInterfacesAsync()).ToList();

        // 3. Look for an interface whose address matches the client's subnet
        if (clientAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            var clientBytes = clientAddress.GetAddressBytes();

            // Try /24 match first, then /16, then /8
            foreach (var iface in interfaces)
            {
                foreach (var addrStr in iface.Addresses)
                {
                    if (IPAddress.TryParse(addrStr, out var localIp) && localIp.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var localBytes = localIp.GetAddressBytes();
                        if (localBytes[0] == clientBytes[0] && localBytes[1] == clientBytes[1] && localBytes[2] == clientBytes[2])
                        {
                            return (localIp, iface.Name);
                        }
                    }
                }
            }

            foreach (var iface in interfaces)
            {
                foreach (var addrStr in iface.Addresses)
                {
                    if (IPAddress.TryParse(addrStr, out var localIp) && localIp.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var localBytes = localIp.GetAddressBytes();
                        if (localBytes[0] == clientBytes[0] && localBytes[1] == clientBytes[1])
                        {
                            return (localIp, iface.Name);
                        }
                    }
                }
            }

            foreach (var iface in interfaces)
            {
                foreach (var addrStr in iface.Addresses)
                {
                    if (IPAddress.TryParse(addrStr, out var localIp) && localIp.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var localBytes = localIp.GetAddressBytes();
                        if (localBytes[0] == clientBytes[0])
                        {
                            return (localIp, iface.Name);
                        }
                    }
                }
            }
        }

        // 4. If no subnet match found, pick the best private non-loopback operational interface
        foreach (var iface in interfaces)
        {
            if (!iface.IsOperational) continue;

            foreach (var addrStr in iface.Addresses)
            {
                if (IPAddress.TryParse(addrStr, out var localIp) &&
                    !IPAddress.IsLoopback(localIp) &&
                    !IsLinkLocal(localIp) &&
                    localIp.AddressFamily == AddressFamily.InterNetwork)
                {
                    return (localIp, iface.Name);
                }
            }
        }

        // 5. Fallback to any usable private IP from network discovery
        var usableIps = (await _networkDiscovery.GetUsablePrivateIpAddressesAsync()).ToList();
        var fallbackIp = usableIps.FirstOrDefault(ip => !IPAddress.IsLoopback(ip) && !IsLinkLocal(ip));
        if (fallbackIp != null)
        {
            return (fallbackIp, null);
        }

        // 6. Last resort
        return (IPAddress.Loopback, "Loopback");
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        return address.IsIPv6LinkLocal;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleLock.Dispose();
    }
}
