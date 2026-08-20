using System.Linq;
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
        var isPublic = config.Listener.Mode == ListenerMode.AllInterfaces || 
                       (config.Listener.Mode == ListenerMode.SpecificAddress && config.Listener.ListenAddress != null && Validation.NetworkValidator.IsListeningOnAllInterfaces(config.Listener.ListenAddress));
        return new SecurityCheck
        {
            Id = "SEC001",
            Name = "No public client access",
            Description = "Proxy should not listen on all interfaces unless required.",
            Passed = !isPublic,
            Level = isPublic ? SecurityLevel.Warning : SecurityLevel.Secure,
            Message = isPublic ? "Sec.Check.Sec001.MsgFail" : "Sec.Check.Sec001.MsgPass",
            Remediation = isPublic ? "Sec.Check.Sec001.Fix" : null
        };
    }

    private static SecurityCheck CheckAllowedClients(ProxyConfiguration config)
    {
        var isAllowAll = config.ClientAccess.Mode == ClientAccessMode.AllowAll;
        var hasClients = config.ClientAccess.AllowedClients.Any(c => c.Enabled);
        return new SecurityCheck
        {
            Id = "SEC002",
            Name = "Client access restricted",
            Description = "Proxy should restrict access to specific clients.",
            Passed = !isAllowAll && hasClients,
            Level = isAllowAll ? SecurityLevel.Warning : (hasClients ? SecurityLevel.Secure : SecurityLevel.Info),
            Message = isAllowAll
                ? "Sec.Check.Sec002.MsgFail"
                : (hasClients ? "Sec.Check.Sec002.MsgPass" : "Sec.Check.Sec002.MsgEmpty"),
            MessageArgs = hasClients ? new[] { config.ClientAccess.AllowedClients.Count(c => c.Enabled).ToString() } : Array.Empty<string>(),
            Remediation = isAllowAll ? "Sec.Check.Sec002.Fix" : (hasClients ? null : "Sec.Check.Sec002.FixEmpty")
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
            Message = "Sec.Check.Sec003.MsgPass"
        };
    }

    private static SecurityCheck CheckFirewallBinding(ProxyConfiguration config)
    {
        var isAllInterfaces = config.Listener.Mode == ListenerMode.AllInterfaces || 
                              (config.Listener.Mode == ListenerMode.SpecificAddress && config.Listener.ListenAddress != null && Validation.NetworkValidator.IsListeningOnAllInterfaces(config.Listener.ListenAddress));
        return new SecurityCheck
        {
            Id = "SEC004",
            Name = "Firewall configuration",
            Description = "Firewall should restrict access to the proxy port.",
            Passed = !isAllInterfaces,
            Level = isAllInterfaces ? SecurityLevel.Warning : SecurityLevel.Secure,
            Message = isAllInterfaces ? "Sec.Check.Sec004.MsgFail" : "Sec.Check.Sec004.MsgPass",
            Remediation = isAllInterfaces ? "Sec.Check.Sec004.Fix" : null
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
            Message = restricted ? "Sec.Check.Sec005.MsgPass" : "Sec.Check.Sec005.MsgFail",
            MessageArgs = restricted ? new[] { string.Join(", ", config.Security.AllowedDestinationPorts) } : Array.Empty<string>(),
            Remediation = restricted ? null : "Sec.Check.Sec005.Fix"
        };
    }

    private static SecurityCheck CheckBroadSubnets(ProxyConfiguration config)
    {
        if (config.ClientAccess.Mode == ClientAccessMode.AllowAll)
        {
            return new SecurityCheck
            {
                Id = "SEC006",
                Name = "No broad subnet rules",
                Description = "Allowed client entries should not be overly broad.",
                Passed = false,
                Level = SecurityLevel.Warning,
                Message = "Sec.Check.Sec006.MsgFailAllowAll",
                Remediation = "Sec.Check.Sec006.FixAllowAll"
            };
        }

        var broadClients = config.ClientAccess.AllowedClients
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
            Message = broadClients.Count > 0 ? "Sec.Check.Sec006.MsgFailBroad" : "Sec.Check.Sec006.MsgPass",
            MessageArgs = broadClients.Count > 0 ? new[] { broadClients.Count.ToString(), string.Join(", ", broadClients.Select(c => c.IpOrCidr)) } : Array.Empty<string>(),
            Remediation = broadClients.Count > 0 ? "Sec.Check.Sec006.FixBroad" : null
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
            Message = errors.Count > 0 ? "Sec.Check.Sec007.MsgFail" : "Sec.Check.Sec007.MsgPass",
            MessageArgs = errors.Count > 0 ? new[] { errors.Count.ToString(), string.Join("; ", errors.Take(3)) } : Array.Empty<string>(),
            Remediation = errors.Count > 0 ? "Sec.Check.Sec007.Fix" : null
        };
    }
}
