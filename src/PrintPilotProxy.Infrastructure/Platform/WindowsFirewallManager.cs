using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// Manages the one firewall rule owned by PrintPilotProxy. It never enumerates,
/// changes, or deletes rules belonging to another application.
/// </summary>
public sealed class WindowsFirewallManager : IPlatformFirewallManager
{
    private static readonly Regex SafeRuleName = new("^[A-Za-z0-9 ._-]+$", RegexOptions.Compiled);
    private readonly ILogger<WindowsFirewallManager> _logger;

    public WindowsFirewallManager(ILogger<WindowsFirewallManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsFirewallManager>.Instance;
    }

    public async Task<bool> CreateRuleAsync(FirewallRule rule, CancellationToken cancellationToken = default)
    {
        ValidateManagedRule(rule);

        // Replacing the stable, product-owned rule keeps updates idempotent and
        // removes an obsolete port/address scope before the new one is added.
        await RemoveRuleAsync(rule.Name, cancellationToken);

        var remoteScope = FormatScope(rule.RemoteAddresses);
        var localScope = FormatScope(rule.LocalAddresses);
        var direction = string.Equals(rule.Direction, "Outbound", StringComparison.OrdinalIgnoreCase) ? "out" : "in";
        var protocol = string.Equals(rule.Protocol, "UDP", StringComparison.OrdinalIgnoreCase) ? "UDP" : "TCP";
        var enabled = rule.Enabled ? "yes" : "no";
        var interfaceScope = NormalizeInterfaceScope(rule.InterfaceScope);

        var arguments =
            $"advfirewall firewall add rule name=\"{rule.Name}\" dir={direction} action=allow " +
            $"protocol={protocol} localport={rule.Port} remoteip=\"{remoteScope}\" " +
            $"localip=\"{localScope}\" interfacetype={interfaceScope} enable={enabled}";

        await RunNetshAsync(arguments, cancellationToken);
        _logger.LogInformation("Updated managed Windows Firewall rule {RuleName} for port {Port}.", rule.Name, rule.Port);
        return true;
    }

    public async Task<bool> RemoveRuleAsync(string ruleName, CancellationToken cancellationToken = default)
    {
        ValidateManagedRuleName(ruleName);
        var existing = await GetStatusAsync(ruleName, cancellationToken);
        if (!existing.RuleExists)
        {
            return true;
        }

        await RunNetshAsync($"advfirewall firewall delete rule name=\"{ruleName}\"", cancellationToken);
        _logger.LogInformation("Removed managed Windows Firewall rule {RuleName}.", ruleName);
        return true;
    }

    public Task<FirewallStatus> GetStatusAsync(string ruleName, CancellationToken cancellationToken = default)
    {
        ValidateManagedRuleName(ruleName);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new FirewallStatus { RuleExists = false, ErrorMessage = "Windows Firewall is only supported on Windows." });
        }

        try
        {
            // HNetCfg exposes structured data regardless of the Windows display
            // language, unlike parsing localized netsh output.
            var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2", throwOnError: true)!;
            dynamic policy = Activator.CreateInstance(policyType)!;
            var firewallEnabled = IsAnyProfileEnabled(policy);

            dynamic nativeRule;
            try
            {
                nativeRule = policy.Rules.Item(ruleName);
            }
            catch (COMException)
            {
                return Task.FromResult(new FirewallStatus { FirewallEnabled = firewallEnabled, RuleExists = false });
            }

            var rule = new FirewallRule
            {
                Name = nativeRule.Name as string ?? ruleName,
                Protocol = ToProtocol((int)nativeRule.Protocol),
                Port = ToPort(nativeRule.LocalPorts as string),
                RemoteAddresses = ParseScope(nativeRule.RemoteAddresses as string),
                LocalAddresses = ParseScope(nativeRule.LocalAddresses as string),
                Enabled = nativeRule.Enabled,
                Direction = (int)nativeRule.Direction == 1 ? "Inbound" : "Outbound",
                Action = (int)nativeRule.Action == 1 ? "Allow" : "Block",
                InterfaceScope = nativeRule.InterfaceTypes as string ?? "Any"
            };

            return Task.FromResult(new FirewallStatus
            {
                FirewallEnabled = firewallEnabled,
                RuleExists = true,
                CurrentRule = rule
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not inspect Windows Firewall status.");
            return Task.FromResult(new FirewallStatus
            {
                RuleExists = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public bool HasPermission
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var identity = WindowsIdentity.GetCurrent();
            if (identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true)
            {
                return true;
            }

            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private static bool IsAnyProfileEnabled(dynamic policy)
        => (bool)policy.FirewallEnabled[1] || (bool)policy.FirewallEnabled[2] || (bool)policy.FirewallEnabled[4];

    private static void ValidateManagedRule(FirewallRule rule)
    {
        ValidateManagedRuleName(rule.Name);
        if (!NetworkValidator.IsValidPort(rule.Port))
        {
            throw new ArgumentOutOfRangeException(nameof(rule.Port), "Firewall rule port must be a valid TCP or UDP port.");
        }

        foreach (var address in rule.RemoteAddresses.Concat(rule.LocalAddresses))
        {
            if (!NetworkValidator.IsValidIpOrCidr(address))
            {
                throw new ArgumentException("Firewall address scopes must be IP addresses or CIDR ranges.");
            }
        }
    }

    private static void ValidateManagedRuleName(string ruleName)
    {
        if (!string.Equals(ruleName, FirewallRuleNames.ManagedRule, StringComparison.Ordinal) ||
            !SafeRuleName.IsMatch(ruleName))
        {
            throw new InvalidOperationException("PrintPilotProxy may manage only its own firewall rule.");
        }
    }

    private static string FormatScope(IEnumerable<string>? addresses)
    {
        var values = addresses?.Where(address => !string.IsNullOrWhiteSpace(address)).ToArray() ?? Array.Empty<string>();
        return values.Length == 0 ? "any" : string.Join(',', values);
    }

    private static List<string> ParseScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope is "*" or "Any")
        {
            return new List<string>();
        }

        return scope.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static int ToPort(string? ports)
        => int.TryParse(ports?.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault(), out var port) ? port : 0;

    private static string ToProtocol(int protocol) => protocol == 17 ? "UDP" : "TCP";

    private static string NormalizeInterfaceScope(string? value)
        => value?.ToLowerInvariant() switch
        {
            "lan" => "lan",
            "wireless" => "wireless",
            "ras" => "ras",
            _ => "any"
        };

    private static async Task RunNetshAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"netsh exited with code {process.ExitCode}: {error} {output}".Trim());
        }
    }
}
