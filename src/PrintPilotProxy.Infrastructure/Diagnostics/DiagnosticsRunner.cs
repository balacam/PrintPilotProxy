using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Diagnostics
{
    public class DiagnosticsRunner : IDiagnosticsRunner
    {
        private readonly ILogger<DiagnosticsRunner> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly IPlatformNetworkManager _networkManager;
        private readonly IPlatformFirewallManager _firewallManager;
        private readonly IProxyEngine _proxyEngine;

        public DiagnosticsRunner(
            ILogger<DiagnosticsRunner> logger,
            IConfigurationManager configManager,
            IPlatformNetworkManager networkManager,
            IPlatformFirewallManager firewallManager,
            IProxyEngine proxyEngine)
        {
            _logger = logger;
            _configManager = configManager;
            _networkManager = networkManager;
            _firewallManager = firewallManager;
            _proxyEngine = proxyEngine;
        }

        public async Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<DiagnosticResult>();

            results.Add(await RunTestAsync("config_valid", cancellationToken));
            results.Add(await RunTestAsync("port_available", cancellationToken));
            results.Add(await RunTestAsync("proxy_running", cancellationToken));
            results.Add(await RunTestAsync("firewall_status", cancellationToken));
            results.Add(await RunTestAsync("internet_connectivity", cancellationToken));
            results.Add(await RunTestAsync("dns_resolution", cancellationToken));

            return results;
        }

        public async Task<DiagnosticResult> RunTestAsync(string testId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Running diagnostic test: {TestId}", testId);
            var result = new DiagnosticResult { Id = testId, Name = testId, Passed = false };

            try
            {
                switch (testId.ToLowerInvariant())
                {
                    case "config_valid":
                        var config = await _configManager.LoadAsync(cancellationToken);
                        var valResult = _configManager.Validate(config);
                        result.Passed = valResult.Count == 0;
                        result.Message = result.Passed ? "Configuration is valid." : string.Join(", ", valResult);
                        result.Remediation = result.Passed ? null : "Update configuration to fix validation errors.";
                        break;
                    case "port_available":
                        var cfg = await _configManager.LoadAsync(cancellationToken);
                        var port = cfg.Listener?.Port ?? 8080;
                        result.Passed = _networkManager.IsPortAvailable(port);
                        result.Message = result.Passed ? $"Port {port} is available." : $"Port {port} is in use.";
                        result.Remediation = result.Passed ? null : "Change the proxy port or stop the conflicting application.";
                        break;
                    case "proxy_running":
                        var engineStatus = _proxyEngine.GetStatus();
                        result.Passed = engineStatus.State == ProxyState.Running;
                        result.Message = result.Passed ? "Proxy engine is running." : $"Proxy engine is {engineStatus.State}.";
                        result.Remediation = result.Passed ? null : "Start the proxy service.";
                        break;
                    case "firewall_status":
                        var cfg2 = await _configManager.LoadAsync(cancellationToken);
                        var port2 = cfg2.Listener?.Port ?? 8080;
                        string ruleName = $"PrintPilotProxy - TCP {port2}";
                        var fwStatus = await _firewallManager.GetStatusAsync(ruleName, cancellationToken);
                        result.Passed = fwStatus.RuleExists;
                        result.Message = result.Passed ? "Firewall rule exists." : "Firewall rule is missing.";
                        result.Remediation = result.Passed ? null : "Create firewall rule using the service manager or CLI.";
                        break;
                    case "internet_connectivity":
                        result.Passed = await _networkManager.TestInternetConnectivityAsync(cancellationToken);
                        result.Message = result.Passed ? "Internet connectivity ok." : "Cannot reach internet.";
                        result.Remediation = result.Passed ? null : "Check network connection or external firewalls.";
                        break;
                    case "dns_resolution":
                        result.Passed = await _networkManager.TestDnsResolutionAsync("dns.google", cancellationToken);
                        result.Message = result.Passed ? "DNS resolution ok." : "DNS resolution failed.";
                        result.Remediation = result.Passed ? null : "Check DNS server settings.";
                        break;
                    default:
                        result.Message = $"Unknown test: {testId}";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test {TestId} failed with exception.", testId);
                result.Passed = false;
                result.Message = $"Exception: {ex.Message}";
                result.Remediation = "Check logs for details.";
            }

            return result;
        }
    }
}
