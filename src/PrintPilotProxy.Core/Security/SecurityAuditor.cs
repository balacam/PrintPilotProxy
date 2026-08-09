using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Security;

/// <summary>
/// Performs security audits on the proxy configuration.
/// </summary>
public sealed class SecurityAuditor : ISecurityAuditor
{
    /// <inheritdoc />
    public SecurityAudit Audit(ProxyConfiguration configuration)
    {
        var audit = new SecurityAudit();

        audit.Checks.Add(CheckPublicAccess(configuration));
        audit.Checks.Add(CheckAllowedClients(configuration));
        audit.Checks.Add(CheckHttpsInterception());
        audit.Checks.Add(CheckFirewallBinding(configuration));
        audit.Checks.Add(CheckDestinationPorts(configuration));
        audit.Checks.Add(CheckBroadSubnets(configuration));
        audit.Checks.Add(CheckConfigurationValid(configuration));

        return audit;
    }

    private static SecurityCheck CheckPublicAccess(ProxyConfiguration config)
    {
        var isPublic = Validation.NetworkValidator.IsListeningOnAllInterfaces(config.Listener.ListenAddress);
        return new SecurityCheck
        {
            Id = "SEC001",
            Name = "No public client access",
            Description = "Proxy should not listen on all interfaces unless required.",
            Passed = !isPublic,
            Level = isPublic ? SecurityLevel.Warning : SecurityLevel.Secure,
            Message = isPublic
                ? "Proxy is listening on all interfaces (0.0.0.0). Consider binding to a specific IP."
                : "Proxy is bound to a specific interface.",
            Remediation = isPublic ? "Set the listen address to a specific network interface IP address." : null
        };
    }

    private static SecurityCheck CheckAllowedClients(ProxyConfiguration config)
    {
        var hasClients = config.AllowedClients.Any(c => c.Enabled);
        return new SecurityCheck
        {
            Id = "SEC002",
            Name = "Allowed clients configured",
            Description = "At least one allowed client must be configured for the proxy to be usable.",
            Passed = hasClients,
            Level = hasClients ? SecurityLevel.Secure : SecurityLevel.Info,
            Message = hasClients
                ? $"{config.AllowedClients.Count(c => c.Enabled)} allowed client(s) configured."
                : "No allowed clients configured. The proxy will reject all connections.",
            Remediation = hasClients ? null : "Add at least one allowed client in the Allowed Clients settings."
        };
    }

    private static SecurityCheck CheckHttpsInterception()
    {
        // PrintPilotProxy never performs HTTPS interception
        return new SecurityCheck
        {
            Id = "SEC003",
            Name = "HTTPS interception disabled",
            Description = "HTTPS traffic is tunneled without inspection, preserving end-to-end encryption.",
            Passed = true,
            Level = SecurityLevel.Secure,
            Message = "HTTPS interception is not implemented. All HTTPS traffic is forwarded via CONNECT tunneling."
        };
    }

    private static SecurityCheck CheckFirewallBinding(ProxyConfiguration config)
    {
        var isAllInterfaces = Validation.NetworkValidator.IsListeningOnAllInterfaces(config.Listener.ListenAddress);
        return new SecurityCheck
        {
            Id = "SEC004",
            Name = "Firewall configuration",
            Description = "Firewall should restrict access to the proxy port.",
            Passed = !isAllInterfaces,
            Level = isAllInterfaces ? SecurityLevel.Warning : SecurityLevel.Secure,
            Message = isAllInterfaces
                ? "Consider configuring Windows Firewall to restrict access to the proxy port."
                : "Proxy is bound to a specific interface.",
            Remediation = isAllInterfaces ? "Configure a Windows Firewall rule to restrict access to allowed client IPs." : null
        };
    }

    private static SecurityCheck CheckDestinationPorts(ProxyConfiguration config)
    {
        var restricted = config.Security.DestinationPortRestrictionsEnabled;
        return new SecurityCheck
        {
            Id = "SEC005",
            Name = "Destination ports restricted",
            Description = "Only specific destination ports should be allowed.",
            Passed = restricted,
            Level = restricted ? SecurityLevel.Secure : SecurityLevel.Warning,
            Message = restricted
                ? $"Destination ports restricted to: {string.Join(", ", config.Security.AllowedDestinationPorts)}"
                : "Destination port restrictions are disabled. Any destination port is allowed.",
            Remediation = restricted ? null : "Enable destination port restrictions in Security settings."
        };
    }

    private static SecurityCheck CheckBroadSubnets(ProxyConfiguration config)
    {
        var broadClients = config.AllowedClients
            .Where(c => c.Enabled && c.IsCidr)
            .Where(c => Validation.NetworkValidator.GetBroadSubnetWarning(c.IpOrCidr) != null)
            .ToList();

        return new SecurityCheck
        {
            Id = "SEC006",
            Name = "No broad subnet rules",
            Description = "Allowed client entries should not be overly broad.",
            Passed = broadClients.Count == 0,
            Level = broadClients.Count > 0 ? SecurityLevel.Warning : SecurityLevel.Secure,
            Message = broadClients.Count > 0
                ? $"{broadClients.Count} broad subnet(s) detected: {string.Join(", ", broadClients.Select(c => c.IpOrCidr))}"
                : "No overly broad subnets configured.",
            Remediation = broadClients.Count > 0 ? "Consider narrowing client CIDR ranges to reduce attack surface." : null
        };
    }

    private static SecurityCheck CheckConfigurationValid(ProxyConfiguration config)
    {
        var errors = Validation.ConfigurationValidator.Validate(config);
        return new SecurityCheck
        {
            Id = "SEC007",
            Name = "Configuration valid",
            Description = "Configuration must pass all validation checks.",
            Passed = errors.Count == 0,
            Level = errors.Count > 0 ? SecurityLevel.Critical : SecurityLevel.Secure,
            Message = errors.Count > 0
                ? $"{errors.Count} configuration error(s): {string.Join("; ", errors.Take(3))}"
                : "Configuration is valid.",
            Remediation = errors.Count > 0 ? "Fix the configuration errors listed above." : null
        };
    }
}
