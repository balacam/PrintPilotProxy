using System.Net;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Diagnostics;

/// <summary>
/// Runs local-only diagnostics. It deliberately performs no remote HTTP,
/// CONNECT, DNS, or internet checks.
/// </summary>
public sealed class DiagnosticsRunner : IDiagnosticsRunner
{
    private readonly ILogger<DiagnosticsRunner> _logger;
    private readonly IConfigurationManager _configurationManager;
    private readonly IPlatformNetworkManager _networkManager;
    private readonly IPlatformFirewallManager _firewallManager;
    private readonly IPlatformServiceManager _serviceManager;
    private readonly INetworkInterfaceDiscovery _networkDiscovery;
    private readonly IProxyEngine _proxyEngine;

    public DiagnosticsRunner(
        ILogger<DiagnosticsRunner>? logger,
        IConfigurationManager configurationManager,
        IPlatformNetworkManager networkManager,
        IPlatformFirewallManager firewallManager,
        IPlatformServiceManager serviceManager,
        INetworkInterfaceDiscovery networkDiscovery,
        IProxyEngine proxyEngine)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DiagnosticsRunner>.Instance;
        _configurationManager = configurationManager;
        _networkManager = networkManager;
        _firewallManager = firewallManager;
        _serviceManager = serviceManager;
        _networkDiscovery = networkDiscovery;
        _proxyEngine = proxyEngine;
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken cancellationToken = default)
    {
        var resultIds = new[]
        {
            "configuration", "service", "ipc", "proxy", "listener", "port", "network_interfaces", "firewall"
        };
        var results = new List<DiagnosticResult>(resultIds.Length);
        foreach (var resultId in resultIds)
        {
            results.Add(await RunTestAsync(resultId, cancellationToken));
        }

        return results;
    }

    public async Task<DiagnosticResult> RunTestAsync(string testId, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var result = testId.ToLowerInvariant() switch
            {
                "configuration" => await CheckConfigurationAsync(cancellationToken),
                "service" => await CheckServiceAsync(cancellationToken),
                "ipc" => CheckIpc(),
                "proxy" => CheckProxy(),
                "listener" => await CheckListenerAsync(cancellationToken),
                "port" => await CheckPortAsync(cancellationToken),
                "network_interfaces" => await CheckNetworkInterfacesAsync(cancellationToken),
                "firewall" => await CheckFirewallAsync(cancellationToken),
                _ => Create(testId, testId, DiagnosticOutcome.Fail, "Unknown diagnostic test.", "Select a supported diagnostic test.")
            };
            result.Duration = DateTimeOffset.UtcNow - started;
            result.TestedAt = DateTimeOffset.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local diagnostic {TestId} failed.", testId);
            var result = Create(testId, testId, DiagnosticOutcome.Fail, ex.Message, "Review the application log for details.");
            result.Duration = DateTimeOffset.UtcNow - started;
            return result;
        }
    }

    private async Task<DiagnosticResult> CheckConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.LoadAsync(cancellationToken);
        var errors = _configurationManager.Validate(configuration);
        return errors.Count == 0
            ? Create("configuration", "Configuration", DiagnosticOutcome.Pass, "Configuration is valid.")
            : Create("configuration", "Configuration", DiagnosticOutcome.Fail, string.Join(" ", errors), "Correct the listed settings and apply again.");
    }

    private async Task<DiagnosticResult> CheckServiceAsync(CancellationToken cancellationToken)
    {
        var service = await _serviceManager.GetInfoAsync(cancellationToken);
        return service.Status switch
        {
            ServiceStatus.Running => Create("service", "Windows Service", DiagnosticOutcome.Pass, "The PrintPilotProxy Windows Service is running."),
            ServiceStatus.Starting or ServiceStatus.Stopping => Create("service", "Windows Service", DiagnosticOutcome.Warning, $"The service is {service.Status}.", "Wait for the transition to finish, then refresh."),
            ServiceStatus.Stopped => Create("service", "Windows Service", DiagnosticOutcome.Warning, "The service is installed but stopped.", "Start the service from the Service page."),
            ServiceStatus.NotInstalled => Create("service", "Windows Service", DiagnosticOutcome.Fail, "The Windows Service is not installed.", "Run the PrintPilotProxy installer."),
            _ => Create("service", "Windows Service", DiagnosticOutcome.Fail, service.ErrorMessage ?? "The service state could not be determined.", "Check Windows Services and the application log.")
        };
    }

    private static DiagnosticResult CheckIpc()
        => Create("ipc", "Named Pipe IPC", DiagnosticOutcome.Pass,
            "The service processed this diagnostics request through its local named pipe.");

    private DiagnosticResult CheckProxy()
    {
        var status = _proxyEngine.GetStatus();
        return status.State == ProxyState.Running
            ? Create("proxy", "Proxy Engine", DiagnosticOutcome.Pass, "The proxy engine is running.")
            : Create("proxy", "Proxy Engine", DiagnosticOutcome.Warning, $"The proxy engine is {status.State}.", "Start the proxy from the Service page if it should be accepting clients.");
    }

    private async Task<DiagnosticResult> CheckListenerAsync(CancellationToken cancellationToken)
    {
        var status = _proxyEngine.GetStatus();
        if (status.State == ProxyState.Running && !string.IsNullOrWhiteSpace(status.ListeningAddress))
        {
            return Create("listener", "Listener", DiagnosticOutcome.Pass, $"Listening on {status.ListeningAddress}.");
        }

        var configuration = await _configurationManager.LoadAsync(cancellationToken);
        if (configuration.Listener.Mode == ListenerMode.SpecificAddress &&
            (!IPAddress.TryParse(configuration.Listener.ListenAddress, out _) || string.IsNullOrWhiteSpace(configuration.Listener.ListenAddress)))
        {
            return Create("listener", "Listener", DiagnosticOutcome.Fail, "The configured listener address is invalid.", "Choose an assigned local address.");
        }

        return Create("listener", "Listener", DiagnosticOutcome.Warning, "The proxy listener is not active.", "Start the proxy to verify its listener binding.");
    }

    private async Task<DiagnosticResult> CheckPortAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.LoadAsync(cancellationToken);
        if (_proxyEngine.GetStatus().State == ProxyState.Running)
        {
            return Create("port", "Proxy Port", DiagnosticOutcome.Pass,
                $"Port {configuration.Listener.Port} is bound by the running proxy.");
        }

        var available = _networkManager.IsPortAvailable(configuration.Listener.Port);
        return available
            ? Create("port", "Proxy Port", DiagnosticOutcome.Pass, $"Port {configuration.Listener.Port} is available.")
            : Create("port", "Proxy Port", DiagnosticOutcome.Fail, $"Port {configuration.Listener.Port} is already in use.", "Choose another port or stop the conflicting process.");
    }

    private async Task<DiagnosticResult> CheckNetworkInterfacesAsync(CancellationToken cancellationToken)
    {
        var interfaces = await _networkDiscovery.GetInterfacesAsync();
        return interfaces.Any()
            ? Create("network_interfaces", "Network Interfaces", DiagnosticOutcome.Pass, $"{interfaces.Count()} usable network adapter(s) detected.")
            : Create("network_interfaces", "Network Interfaces", DiagnosticOutcome.Warning, "No usable network adapter is currently connected.", "Connect a network adapter before starting Automatic listener mode.");
    }

    private async Task<DiagnosticResult> CheckFirewallAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.LoadAsync(cancellationToken);
        var status = await _firewallManager.GetStatusAsync(FirewallRuleNames.ManagedRule, cancellationToken);
        if (status.ErrorMessage is not null)
        {
            return Create("firewall", "Windows Firewall", DiagnosticOutcome.Warning, status.ErrorMessage, "Review Windows Firewall availability and permissions.");
        }

        if (!configuration.Firewall.RuleEnabled)
        {
            return status.RuleExists
                ? Create("firewall", "Windows Firewall", DiagnosticOutcome.Warning, "Firewall management is disabled but a managed rule still exists.", "Remove the managed rule from the Firewall page.")
                : Create("firewall", "Windows Firewall", DiagnosticOutcome.Pass, "Firewall management is disabled and no managed rule exists.");
        }

        if (!status.FirewallEnabled)
        {
            return Create("firewall", "Windows Firewall", DiagnosticOutcome.Warning, "Windows Defender Firewall is disabled on all profiles.", "Enable and review Windows Firewall according to your organisation's policy.");
        }

        return status.RuleExists
            ? Create("firewall", "Windows Firewall", DiagnosticOutcome.Pass, "The PrintPilotProxy firewall rule exists.")
            : Create("firewall", "Windows Firewall", DiagnosticOutcome.Warning, "The PrintPilotProxy firewall rule is missing.", "Create or update the rule from the Firewall page.");
    }

    private static DiagnosticResult Create(string id, string name, DiagnosticOutcome outcome, string message, string? remediation = null)
        => new()
        {
            Id = id,
            Name = name,
            Description = name,
            Outcome = outcome,
            Passed = outcome == DiagnosticOutcome.Pass,
            Message = message,
            Remediation = remediation
        };
}
