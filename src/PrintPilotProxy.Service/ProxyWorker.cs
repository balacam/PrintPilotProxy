using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using IConfigurationManager = PrintPilotProxy.Core.Interfaces.IConfigurationManager;

namespace PrintPilotProxy.Service;

/// <summary>Hosts the proxy engine and its local named-pipe management API.</summary>
public sealed class ProxyWorker : BackgroundService
{
    private const int MaximumAutomaticRecoveryAttempts = 3;
    private readonly ILogger<ProxyWorker> _logger;
    private readonly IProxyEngine _proxyEngine;
    private readonly IConfigurationManager _configManager;
    private readonly IPlatformFirewallManager _firewallManager;
    private readonly IPlatformNetworkManager _networkManager;
    private readonly INetworkInterfaceDiscovery _networkDiscovery;
    private readonly IIpcServer _ipcServer;
    private readonly IAccessControlList _acl;
    private readonly IDiagnosticsRunner _diagnostics;
    private readonly ISecurityAuditor _securityAuditor;
    private readonly IIpcSecurityValidator? _securityValidator;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ProxyWorker(
        ILogger<ProxyWorker> logger,
        IProxyEngine proxyEngine,
        IConfigurationManager configManager,
        IPlatformFirewallManager firewallManager,
        IPlatformNetworkManager networkManager,
        INetworkInterfaceDiscovery networkDiscovery,
        IIpcServer ipcServer,
        IAccessControlList acl,
        IDiagnosticsRunner diagnostics,
        ISecurityAuditor securityAuditor,
        IIpcSecurityValidator? securityValidator = null)
    {
        _logger = logger;
        _proxyEngine = proxyEngine;
        _configManager = configManager;
        _firewallManager = firewallManager;
        _networkManager = networkManager;
        _networkDiscovery = networkDiscovery;
        _ipcServer = ipcServer;
        _acl = acl;
        _diagnostics = diagnostics;
        _securityAuditor = securityAuditor;
        _securityValidator = securityValidator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PrintPilotProxy service worker starting.");
        _ipcServer.MessageReceived += HandleIpcMessageAsync;
        _proxyEngine.RequestProcessed += OnRequestProcessed;

        try
        {
            await _ipcServer.StartAsync(stoppingToken);
            var configuration = await _configManager.LoadAsync(stoppingToken);
            _acl.Refresh(configuration);

            if (configuration.Service.AutoStartProxy)
            {
                await StartConfiguredProxyWithRecoveryAsync(configuration, stoppingToken);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during service shutdown.
        }
        catch (Exception ex)
        {
            // Keep the Windows Service alive when the engine fails. The IPC
            // endpoint remains usable so an administrator can inspect and fix
            // the configuration instead of being left with a restart loop.
            _logger.LogCritical(ex, "PrintPilotProxy service worker stopped unexpectedly.");
        }
        finally
        {
            _proxyEngine.RequestProcessed -= OnRequestProcessed;
            _ipcServer.MessageReceived -= HandleIpcMessageAsync;

            if (_proxyEngine.GetStatus().State is ProxyState.Running or ProxyState.Starting or ProxyState.Faulted)
            {
                try
                {
                    await _proxyEngine.StopAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping proxy engine during service shutdown.");
                }
            }

            await _ipcServer.StopAsync(CancellationToken.None);
            _logger.LogInformation("PrintPilotProxy service worker stopped.");
        }
    }

    private async Task StartConfiguredProxyWithRecoveryAsync(ProxyConfiguration configuration, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ValidateRuntimeConfigurationAsync(configuration, cancellationToken);
                await ConfigureFirewallAsync(configuration, cancellationToken);
                _acl.Refresh(configuration);
                await _proxyEngine.StartAsync(configuration, cancellationToken);
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                attempts++;
                _logger.LogError(ex, "Proxy engine start attempt {Attempt} failed.", attempts);

                if (!configuration.Service.AutoRestartOnFailure || attempts >= MaximumAutomaticRecoveryAttempts)
                {
                    _logger.LogError(
                        "Proxy engine recovery stopped after {AttemptCount} attempt(s). " +
                        "The Windows Service remains available for local administration.", attempts);
                    return;
                }

                var baseDelay = Math.Clamp(configuration.Service.RestartDelaySeconds, 1, 300);
                var delaySeconds = Math.Min(baseDelay * (1 << (attempts - 1)), 300);
                _logger.LogWarning("Retrying proxy engine start in {DelaySeconds} seconds.", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
    }

    private void OnRequestProcessed(object? sender, ProxyRequestEntry request)
    {
        _logger.LogInformation("Proxy request: {ClientIp} {Method} {Destination} - {StatusCode}",
            request.ClientIp, request.Method, request.Destination, request.StatusCode);
    }

    private async Task<IpcMessage> HandleIpcMessageAsync(IpcMessage request)
    {
        try
        {
            var clientIdentity = request.ClientIdentity ?? IpcClientIdentity.CreateInteractiveUser();
            if (_securityValidator is not null && !_securityValidator.IsAuthorized(clientIdentity, request.Type, out var failureReason))
            {
                return Error(request, failureReason ?? "Access denied: Unauthorized IPC caller.");
            }

            return request.Type switch
            {
                IpcMessageTypes.GetStatus => HandleGetStatus(request),
                IpcMessageTypes.StartProxy => await HandleStartProxyAsync(request),
                IpcMessageTypes.StopProxy => await HandleStopProxyAsync(request),
                IpcMessageTypes.RestartProxy => await HandleRestartProxyAsync(request),
                IpcMessageTypes.GetConfiguration => await HandleGetConfigurationAsync(request),
                IpcMessageTypes.UpdateConfiguration => await HandleUpdateConfigurationAsync(request),
                IpcMessageTypes.GetRecentRequests => HandleGetRecentRequests(request),
                IpcMessageTypes.GetNetworkInterfaces => await HandleGetNetworkInterfacesAsync(request),
                IpcMessageTypes.GetFirewallStatus => await HandleGetFirewallStatusAsync(request),
                IpcMessageTypes.ApplyFirewallRule => await HandleApplyFirewallRuleAsync(request),
                IpcMessageTypes.RemoveFirewallRule => await HandleRemoveFirewallRuleAsync(request),
                IpcMessageTypes.RunDiagnostics => await HandleRunDiagnosticsAsync(request),
                IpcMessageTypes.GetSecurityAudit => await HandleGetSecurityAuditAsync(request),
                _ => Error(request, "Unknown management request.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC request {RequestType}.", request.Type);
            return Error(request, ex.Message);
        }
    }

    private IpcMessage HandleGetStatus(IpcMessage request) => new()
    {
        Type = IpcMessageTypes.StatusResponse,
        CorrelationId = request.CorrelationId,
        Payload = JsonSerializer.Serialize(_proxyEngine.GetStatus(), _jsonOptions)
    };

    private async Task<IpcMessage> HandleStartProxyAsync(IpcMessage request)
    {
        var status = _proxyEngine.GetStatus();
        if (status.State is ProxyState.Running or ProxyState.Starting)
        {
            return Success(request, "Proxy is already running.");
        }

        var configuration = await _configManager.LoadAsync();
        await ValidateRuntimeConfigurationAsync(configuration, CancellationToken.None);
        await ConfigureFirewallAsync(configuration, CancellationToken.None);
        _acl.Refresh(configuration);
        await _proxyEngine.StartAsync(configuration);
        return Success(request, "Proxy started.");
    }

    private async Task<IpcMessage> HandleStopProxyAsync(IpcMessage request)
    {
        await _proxyEngine.StopAsync();
        return Success(request, "Proxy stopped.");
    }

    private async Task<IpcMessage> HandleRestartProxyAsync(IpcMessage request)
    {
        if (_proxyEngine.GetStatus().State is ProxyState.Running or ProxyState.Starting or ProxyState.Faulted)
        {
            await _proxyEngine.StopAsync();
        }

        var configuration = await _configManager.LoadAsync();
        await ValidateRuntimeConfigurationAsync(configuration, CancellationToken.None);
        await ConfigureFirewallAsync(configuration, CancellationToken.None);
        _acl.Refresh(configuration);
        await _proxyEngine.StartAsync(configuration);
        return Success(request, "Proxy restarted.");
    }

    private async Task<IpcMessage> HandleGetConfigurationAsync(IpcMessage request)
    {
        var configuration = await _configManager.LoadAsync();
        return new IpcMessage
        {
            Type = IpcMessageTypes.ConfigurationResponse,
            CorrelationId = request.CorrelationId,
            Payload = JsonSerializer.Serialize(configuration, _jsonOptions)
        };
    }

    private async Task<IpcMessage> HandleUpdateConfigurationAsync(IpcMessage request)
    {
        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            return Error(request, "A configuration payload is required.");
        }

        var updatedConfiguration = JsonSerializer.Deserialize<ProxyConfiguration>(request.Payload, _jsonOptions);
        if (updatedConfiguration is null)
        {
            return Error(request, "The configuration payload is invalid.");
        }

        var validationErrors = _configManager.Validate(updatedConfiguration);
        if (validationErrors.Count > 0)
        {
            return Error(request, string.Join(" ", validationErrors));
        }

        var previousConfiguration = await _configManager.LoadAsync();
        string backupPath;
        try
        {
            backupPath = await _configManager.BackupAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration apply cancelled because the pre-change backup could not be created.");
            return Error(request, "Could not create a configuration backup. No changes were applied.");
        }

        var wasRunning = _proxyEngine.GetStatus().State == ProxyState.Running;
        var requiresRestart = RequiresProxyRestart(previousConfiguration, updatedConfiguration);

        try
        {
            if (wasRunning && requiresRestart)
            {
                await _proxyEngine.StopAsync();
            }

            if (requiresRestart || !wasRunning)
            {
                await ValidateRuntimeConfigurationAsync(updatedConfiguration, CancellationToken.None);
            }
            await _configManager.SaveAsync(updatedConfiguration);
            _acl.Refresh(updatedConfiguration);
            await ConfigureFirewallAsync(updatedConfiguration, CancellationToken.None);

            if (wasRunning && requiresRestart)
            {
                await _proxyEngine.StartAsync(updatedConfiguration);
                if (_proxyEngine.GetStatus().State != ProxyState.Running)
                {
                    throw new InvalidOperationException("Proxy engine did not enter the Running state after configuration apply.");
                }
            }

            return Success(request, requiresRestart
                ? "Configuration applied and proxy restarted."
                : "Configuration applied.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration apply failed; restoring backup {BackupPath}.", backupPath);
            try
            {
                var restoredConfiguration = await _configManager.RestoreAsync(backupPath);
                _acl.Refresh(restoredConfiguration);
                await ConfigureFirewallAsync(restoredConfiguration, CancellationToken.None);
                if (wasRunning && requiresRestart)
                {
                    await _proxyEngine.StartAsync(restoredConfiguration);
                }

                return Error(request, $"Configuration apply failed and the previous configuration was restored: {ex.Message}");
            }
            catch (Exception restoreException)
            {
                _logger.LogCritical(restoreException, "Configuration restore failed after an apply error.");
                return Error(request, $"Configuration apply failed and automatic restore failed: {ex.Message}");
            }
        }
    }

    private IpcMessage HandleGetRecentRequests(IpcMessage request) => new()
    {
        Type = IpcMessageTypes.RecentRequestsResponse,
        CorrelationId = request.CorrelationId,
        Payload = JsonSerializer.Serialize(_proxyEngine.GetRecentRequests(100), _jsonOptions)
    };

    private async Task<IpcMessage> HandleGetNetworkInterfacesAsync(IpcMessage request)
    {
        var interfaces = await _networkDiscovery.GetInterfacesAsync();
        return new IpcMessage
        {
            Type = IpcMessageTypes.NetworkInterfacesResponse,
            CorrelationId = request.CorrelationId,
            Payload = JsonSerializer.Serialize(interfaces, _jsonOptions)
        };
    }

    private async Task<IpcMessage> HandleGetFirewallStatusAsync(IpcMessage request)
    {
        var status = await _firewallManager.GetStatusAsync(FirewallRuleNames.ManagedRule);
        return new IpcMessage
        {
            Type = IpcMessageTypes.FirewallStatusResponse,
            CorrelationId = request.CorrelationId,
            Payload = JsonSerializer.Serialize(status, _jsonOptions)
        };
    }

    private async Task<IpcMessage> HandleApplyFirewallRuleAsync(IpcMessage request)
    {
        var configuration = await _configManager.LoadAsync();
        configuration.Firewall.RuleEnabled = true;
        await _configManager.SaveAsync(configuration);
        await ConfigureFirewallAsync(configuration, CancellationToken.None);
        return Success(request, "Firewall rule created or updated.");
    }

    private async Task<IpcMessage> HandleRemoveFirewallRuleAsync(IpcMessage request)
    {
        var configuration = await _configManager.LoadAsync();
        configuration.Firewall.RuleEnabled = false;
        await _configManager.SaveAsync(configuration);
        await _firewallManager.RemoveRuleAsync(FirewallRuleNames.ManagedRule);
        return Success(request, "Firewall rule removed.");
    }

    private async Task<IpcMessage> HandleRunDiagnosticsAsync(IpcMessage request)
    {
        var results = await _diagnostics.RunAllAsync();
        return new IpcMessage
        {
            Type = IpcMessageTypes.DiagnosticsResponse,
            CorrelationId = request.CorrelationId,
            Payload = JsonSerializer.Serialize(results, _jsonOptions)
        };
    }

    private async Task<IpcMessage> HandleGetSecurityAuditAsync(IpcMessage request)
    {
        var configuration = await _configManager.LoadAsync();
        return new IpcMessage
        {
            Type = IpcMessageTypes.SecurityAuditResponse,
            CorrelationId = request.CorrelationId,
            Payload = JsonSerializer.Serialize(_securityAuditor.Audit(configuration), _jsonOptions)
        };
    }

    private async Task ValidateRuntimeConfigurationAsync(ProxyConfiguration configuration, CancellationToken cancellationToken)
    {
        var candidateAddresses = await ResolveListenerAddressesAsync(configuration, cancellationToken);
        foreach (var address in candidateAddresses)
        {
            if (!_networkManager.IsPortAvailable(configuration.Listener.Port, address.ToString()))
            {
                throw new InvalidOperationException($"Proxy port {configuration.Listener.Port} is unavailable on {address}.");
            }
        }
    }

    private async Task<List<IPAddress>> ResolveListenerAddressesAsync(ProxyConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var interfaces = await _networkDiscovery.GetInterfacesAsync();

        return configuration.Listener.Mode switch
        {
            ListenerMode.Auto => interfaces.SelectMany(networkInterface => networkInterface.Addresses)
                .Select(s => IPAddress.TryParse(s, out var ip) ? ip : null)
                .Where(ip => ip != null)
                .Select(ip => ip!)
                .Distinct()
                .ToList() switch
            {
                { Count: > 0 } addresses => addresses,
                _ => throw new InvalidOperationException("Automatic listener mode found no usable local network address.")
            },
            ListenerMode.SpecificAdapter => ResolveSpecificAdapter(interfaces, configuration.Listener.AdapterName),
            ListenerMode.SpecificAddress => ResolveSpecificAddress(interfaces, configuration.Listener.ListenAddress),
            ListenerMode.AllInterfaces => new List<IPAddress> { IPAddress.Any },
            _ => throw new InvalidOperationException("Unsupported listener mode.")
        };
    }

    private static List<IPAddress> ResolveSpecificAdapter(
        IEnumerable<DiscoveredNetworkInterface> interfaces, string? adapterName)
    {
        var addresses = interfaces
            .FirstOrDefault(networkInterface => string.Equals(networkInterface.Name, adapterName, StringComparison.OrdinalIgnoreCase))
            ?.Addresses
            .Select(s => IPAddress.TryParse(s, out var ip) ? ip : null)
            .Where(ip => ip != null)
            .Select(ip => ip!)
            .ToList();
        return addresses is { Count: > 0 }
            ? addresses
            : throw new InvalidOperationException("The selected network adapter is unavailable or has no usable IP address.");
    }

    private static List<IPAddress> ResolveSpecificAddress(
        IEnumerable<DiscoveredNetworkInterface> interfaces, string? listenerAddress)
    {
        if (!IPAddress.TryParse(listenerAddress, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            throw new InvalidOperationException("The selected listener address is invalid.");
        }

        if (IPAddress.IsLoopback(address) || interfaces.SelectMany(networkInterface => networkInterface.Addresses).Any(localAddress => localAddress.Equals(address)))
        {
            return new List<IPAddress> { address };
        }

        throw new InvalidOperationException("The selected listener address is not assigned to this computer.");
    }

    private async Task ConfigureFirewallAsync(ProxyConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            if (!configuration.Firewall.RuleEnabled)
            {
                if (_firewallManager.HasPermission)
                {
                    await _firewallManager.RemoveRuleAsync(FirewallRuleNames.ManagedRule, cancellationToken);
                }
                return;
            }

            if (!_firewallManager.HasPermission)
            {
                _logger.LogWarning("Skipping Windows Firewall rule configuration because process lacks administrator permission to manage Windows Firewall.");
                return;
            }

            var localAddresses = configuration.Listener.Mode == ListenerMode.AllInterfaces
                ? new List<string>()
                : (await ResolveListenerAddressesAsync(configuration, cancellationToken))
                    .Select(address => address.ToString())
                    .ToList();
            var remoteAddresses = configuration.ClientAccess.Mode == ClientAccessMode.AllowList
                ? configuration.ClientAccess.AllowedClients
                    .Where(client => client.Enabled)
                    .Select(client => client.IpOrCidr)
                    .ToList()
                : new List<string>();

            await _firewallManager.CreateRuleAsync(new FirewallRule
            {
                Name = FirewallRuleNames.ManagedRule,
                Protocol = "TCP",
                Port = configuration.Listener.Port,
                Direction = "Inbound",
                Action = "Allow",
                LocalAddresses = localAddresses,
                RemoteAddresses = remoteAddresses,
                InterfaceScope = configuration.Firewall.InterfaceScope
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Windows Firewall rule during configuration sync.");
        }
    }

    private static bool RequiresProxyRestart(ProxyConfiguration before, ProxyConfiguration after)
        => before.Listener.Mode != after.Listener.Mode ||
           before.Listener.ListenAddress != after.Listener.ListenAddress ||
           before.Listener.AdapterName != after.Listener.AdapterName ||
           before.Listener.Port != after.Listener.Port ||
           before.Listener.MaxConnections != after.Listener.MaxConnections ||
           before.Listener.ConnectionTimeoutSeconds != after.Listener.ConnectionTimeoutSeconds ||
           before.ClientAccess.Mode != after.ClientAccess.Mode ||
           !before.ClientAccess.AllowedClients.Select(client => (client.Id, client.Name, client.IpOrCidr, client.Enabled))
               .SequenceEqual(after.ClientAccess.AllowedClients.Select(client => (client.Id, client.Name, client.IpOrCidr, client.Enabled))) ||
           before.Security.DestinationPortRestrictionsEnabled != after.Security.DestinationPortRestrictionsEnabled ||
           !before.Security.AllowedDestinationPorts.SequenceEqual(after.Security.AllowedDestinationPorts);

    private static IpcMessage Success(IpcMessage request, string message) => new()
    {
        Type = IpcMessageTypes.Success,
        CorrelationId = request.CorrelationId,
        Payload = message
    };

    private static IpcMessage Error(IpcMessage request, string message) => new()
    {
        Type = IpcMessageTypes.Error,
        CorrelationId = request.CorrelationId,
        Payload = message
    };
}
