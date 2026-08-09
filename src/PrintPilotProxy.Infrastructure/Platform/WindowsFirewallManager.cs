using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform
{
    public class WindowsFirewallManager : IPlatformFirewallManager
    {
        private readonly ILogger<WindowsFirewallManager> _logger;

        public WindowsFirewallManager(ILogger<WindowsFirewallManager> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CreateRuleAsync(FirewallRule rule, CancellationToken cancellationToken = default)
        {
            try
            {
                // Mitigate command injection by stripping quotes
                var safeName = rule.Name.Replace("\"", "");
                var safeRemoteIps = string.Join(",", rule.RemoteAddresses).Replace("\"", "");
                string args = $"advfirewall firewall add rule name=\"{safeName}\" dir={(rule.Direction == "Inbound" ? "in" : "out")} action=allow protocol={rule.Protocol} localport={rule.Port} remoteip=\"{safeRemoteIps}\"";
                await RunNetshAsync(args, cancellationToken);
                _logger.LogInformation("Firewall rule created for port {Port}", rule.Port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create firewall rule.");
                throw;
            }
        }

        public async Task<bool> RemoveRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            try
            {
                var safeRuleName = ruleName.Replace("\"", "");
                string args = $"advfirewall firewall delete rule name=\"{safeRuleName}\"";
                await RunNetshAsync(args, cancellationToken);
                _logger.LogInformation("Firewall rule removed: {RuleName}", ruleName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove firewall rule.");
                throw;
            }
        }

        public async Task<FirewallStatus> GetStatusAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            try
            {
                var safeRuleName = ruleName.Replace("\"", "");
                string args = $"advfirewall firewall show rule name=\"{safeRuleName}\"";
                var output = await RunNetshWithOutputAsync(args, cancellationToken);
                
                return new FirewallStatus
                {
                    RuleExists = output.Contains(safeRuleName, StringComparison.OrdinalIgnoreCase)
                };
            }
            catch (Exception)
            {
                return new FirewallStatus { RuleExists = false };
            }
        }

        public bool HasPermission
        {
            get
            {
#pragma warning disable CA1416
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
            }
        }

        private Task RunNetshAsync(string arguments, CancellationToken cancellationToken)
        {
            return RunNetshWithOutputAsync(arguments, cancellationToken);
        }

        private async Task<string> RunNetshWithOutputAsync(string arguments, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 && !error.Contains("No rules match the specified criteria"))
            {
                throw new InvalidOperationException($"netsh error: {error}. Output: {output}");
            }

            return output;
        }
    }
}
