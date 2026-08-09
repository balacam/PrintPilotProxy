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

    /// <summary>Detailed result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Remediation advice if the test failed.</summary>
    public string? Remediation { get; set; }

    /// <summary>Time taken to run the test.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>When the test was run.</summary>
    public DateTimeOffset TestedAt { get; set; } = DateTimeOffset.UtcNow;
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

    /// <summary>Whether the rule is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Direction (Inbound/Outbound).</summary>
    public string Direction { get; set; } = "Inbound";

    /// <summary>Action (Allow/Block).</summary>
    public string Action { get; set; } = "Allow";
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
