using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// Used where the proxy does not manage the host firewall (Linux, macOS). Linux distributions ship different
/// front ends (ufw, firewalld, nftables), so the operator opens TCP 3128 and UDP 37421 with their own tool; see
/// docs/LINUX.md. Reports honestly that nothing is managed instead of claiming a rule exists.
/// </summary>
public sealed class UnsupportedFirewallManager : IPlatformFirewallManager
{
    private readonly ILogger<UnsupportedFirewallManager> _logger;

    public UnsupportedFirewallManager(ILogger<UnsupportedFirewallManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UnsupportedFirewallManager>.Instance;
    }

    public bool HasPermission => false;

    public Task<bool> CreateRuleAsync(FirewallRule rule, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Firewall management is not available on this platform; open {Protocol} {Port} manually.", rule.Protocol, rule.Port);
        return Task.FromResult(false);
    }

    public Task<bool> RemoveRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<FirewallStatus> GetStatusAsync(string ruleName, CancellationToken cancellationToken = default)
        => Task.FromResult(new FirewallStatus
        {
            FirewallEnabled = false,
            RuleExists = false,
            ErrorMessage = "The host firewall is not managed by PrintPilotProxy on this platform. Open TCP 3128 and UDP 37421 with ufw, firewalld or nftables."
        });
}
