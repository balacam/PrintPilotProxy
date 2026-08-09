namespace PrintPilotProxy.Core.Models;

/// <summary>
/// The main configuration model for PrintPilotProxy.
/// Platform-independent. Serialized to/from JSON.
/// </summary>
public sealed class ProxyConfiguration
{
    /// <summary>
    /// Configuration schema version for forward compatibility.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Proxy listener settings.
    /// </summary>
    public ListenerSettings Listener { get; set; } = new();

    /// <summary>
    /// Allowed client entries (ACL).
    /// </summary>
    public List<AllowedClient> AllowedClients { get; set; } = new();

    /// <summary>
    /// Security-related settings.
    /// </summary>
    public SecuritySettings Security { get; set; } = new();

    /// <summary>
    /// Logging settings.
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();

    /// <summary>
    /// Service behavior settings.
    /// </summary>
    public ServiceSettings Service { get; set; } = new();

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Proxy listener configuration.
/// </summary>
public sealed class ListenerSettings
{
    /// <summary>
    /// IP address to listen on (e.g., "192.168.10.10" or "0.0.0.0").
    /// </summary>
    public string ListenAddress { get; set; } = System.Net.IPAddress.Loopback.ToString();

    /// <summary>
    /// Port to listen on. Default: 3128.
    /// </summary>
    public int Port { get; set; } = 3128;

    /// <summary>
    /// Maximum number of concurrent connections.
    /// </summary>
    public int MaxConnections { get; set; } = 1000;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Security-related configuration.
/// </summary>
public sealed class SecuritySettings
{
    /// <summary>
    /// Allowed destination ports. Only traffic to these ports will be forwarded.
    /// Default: 80, 443.
    /// </summary>
    public List<int> AllowedDestinationPorts { get; set; } = new() { 80, 443 };

    /// <summary>
    /// Whether destination port restrictions are enabled.
    /// When false, all destination ports are allowed (less secure).
    /// </summary>
    public bool DestinationPortRestrictionsEnabled { get; set; } = true;

    /// <summary>
    /// Whether proxy authentication is required (optional, in addition to IP ACL).
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;
}

/// <summary>
/// Logging configuration.
/// </summary>
public sealed class LoggingSettings
{
    /// <summary>
    /// Whether request logging is enabled.
    /// </summary>
    public bool RequestLoggingEnabled { get; set; } = true;

    /// <summary>
    /// Log retention in days. Default: 30.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Maximum total log size in megabytes. Default: 100.
    /// </summary>
    public int MaxSizeMb { get; set; } = 100;

    /// <summary>
    /// Minimum log level. Default: "Information".
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";
}

/// <summary>
/// Windows Service behavior settings.
/// </summary>
public sealed class ServiceSettings
{
    /// <summary>
    /// Whether the proxy should start automatically when the service starts.
    /// </summary>
    public bool AutoStartProxy { get; set; } = true;

    /// <summary>
    /// Whether to automatically restart the proxy engine if it crashes.
    /// </summary>
    public bool AutoRestartOnFailure { get; set; } = true;

    /// <summary>
    /// Delay in seconds before restarting the proxy after a failure.
    /// </summary>
    public int RestartDelaySeconds { get; set; } = 5;
}
