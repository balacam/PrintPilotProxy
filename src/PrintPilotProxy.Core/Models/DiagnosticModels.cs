namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Represents a diagnostic test result.
/// </summary>
public sealed class DiagnosticResult
{
    /// <summary>Test identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name of the diagnostic test.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what the test verifies.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the test passed.</summary>
    public bool Passed { get; set; }

    /// <summary>Distinguishes a non-fatal warning from an actual failed check.</summary>
    public DiagnosticOutcome Outcome { get; set; } = DiagnosticOutcome.Pass;

    /// <summary>Detailed result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Remediation advice if the test failed.</summary>
    public string? Remediation { get; set; }

    /// <summary>Time taken to run the test.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>When the test was run.</summary>
    public DateTimeOffset TestedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum DiagnosticOutcome
{
    Pass,
    Warning,
    Fail
}

/// <summary>
/// Represents a network interface available on the system.
/// </summary>
public sealed class NetworkInterfaceInfo
{
    /// <summary>Interface name (e.g., "Ethernet").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Interface description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>IPv4 addresses assigned to this interface.</summary>
    public List<string> IPv4Addresses { get; set; } = new();

    /// <summary>IPv6 addresses assigned to this interface.</summary>
    public List<string> IPv6Addresses { get; set; } = new();

    /// <summary>Whether the interface is currently connected/up.</summary>
    public bool IsUp { get; set; }

    /// <summary>Interface type (Ethernet, WiFi, Loopback, etc.).</summary>
    public string InterfaceType { get; set; } = string.Empty;
}

/// <summary>
/// Represents a Windows Firewall rule.
/// </summary>
public sealed class FirewallRule
{
    /// <summary>Rule name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Protocol (TCP, UDP).</summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>Local port.</summary>
    public int Port { get; set; }

    /// <summary>Remote addresses allowed (CIDR or specific IPs).</summary>
    public List<string> RemoteAddresses { get; set; } = new();

    /// <summary>Local addresses allowed.</summary>
    public List<string> LocalAddresses { get; set; } = new();

    /// <summary>Whether the rule is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Direction (Inbound/Outbound).</summary>
    public string Direction { get; set; } = "Inbound";

    /// <summary>Action (Allow/Block).</summary>
    public string Action { get; set; } = "Allow";

    /// <summary>Windows interface scope (LAN, Wireless, RAS, or Any).</summary>
    public string InterfaceScope { get; set; } = "Any";

    /// <summary>Firewall profile scope (Private, Domain, Public, or Any).</summary>
    public string Profile { get; set; } = "Private";

    /// <summary>The absolute path to the executable to which this rule applies.</summary>
    public string? Program { get; set; }
}

/// <summary>
/// Names reserved for firewall rules owned by PrintPilotProxy. Keeping stable
/// names makes updates idempotent and guarantees that uninstall removes
/// only rules created by this product.
/// </summary>
public static class FirewallRuleNames
{
    public const string ManagedRule = "PrintPilotProxy";
    public const string DiscoveryRule = "PrintPilotProxy Discovery (UDP-In)";
}

/// <summary>
/// Status of the firewall configuration.
/// </summary>
public sealed class FirewallStatus
{
    /// <summary>Whether the firewall is enabled on the system.</summary>
    public bool FirewallEnabled { get; set; }

    /// <summary>Whether a PrintPilotProxy rule exists.</summary>
    public bool RuleExists { get; set; }

    /// <summary>The current rule, if it exists.</summary>
    public FirewallRule? CurrentRule { get; set; }

    /// <summary>Any error message.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a platform service (Windows Service / systemd).
/// </summary>
public enum ServiceStatus
{
    /// <summary>Service status is unknown.</summary>
    Unknown,

    /// <summary>Service is not installed.</summary>
    NotInstalled,

    /// <summary>Service is installed but stopped.</summary>
    Stopped,

    /// <summary>Service is starting.</summary>
    Starting,

    /// <summary>Service is running.</summary>
    Running,

    /// <summary>Service is stopping.</summary>
    Stopping,

    /// <summary>Service is paused.</summary>
    Paused
}

/// <summary>Configured Windows Service start mode.</summary>
public enum ServiceStartupType
{
    Unknown,
    Automatic,
    AutomaticDelayed,
    Manual,
    Disabled
}

/// <summary>Actual service-control-manager state exposed to the administration UI.</summary>
public sealed class PlatformServiceInfo
{
    public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;
    public ServiceStartupType StartupType { get; set; } = ServiceStartupType.Unknown;
    public string? ErrorMessage { get; set; }
}
