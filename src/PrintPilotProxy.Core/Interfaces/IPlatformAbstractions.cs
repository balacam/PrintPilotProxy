using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Platform-specific service manager (Windows Service or systemd).
/// </summary>
public interface IPlatformServiceManager
{
    /// <summary>Installs the service.</summary>
    Task<bool> InstallServiceAsync(CancellationToken cancellationToken = default);

    /// <summary>Uninstalls the service.</summary>
    Task<bool> UninstallServiceAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current service status.</summary>
    Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts the service.</summary>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the service.</summary>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Restarts the service.</summary>
    Task<bool> RestartAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Platform-specific firewall manager.
/// </summary>
public interface IPlatformFirewallManager
{
    /// <summary>Creates or updates a firewall rule for the proxy.</summary>
    Task<bool> CreateRuleAsync(FirewallRule rule, CancellationToken cancellationToken = default);

    /// <summary>Removes the proxy firewall rule.</summary>
    Task<bool> RemoveRuleAsync(string ruleName, CancellationToken cancellationToken = default);

    /// <summary>Gets the current firewall status.</summary>
    Task<FirewallStatus> GetStatusAsync(string ruleName, CancellationToken cancellationToken = default);

    /// <summary>Whether the current process has permission to modify firewall rules.</summary>
    bool HasPermission { get; }
}

/// <summary>
/// Platform-specific network interface discovery.
/// </summary>
public interface IPlatformNetworkManager
{
    /// <summary>Gets all available network interfaces.</summary>
    IReadOnlyList<NetworkInterfaceInfo> GetInterfaces();

    /// <summary>Checks whether a port is available for binding.</summary>
    bool IsPortAvailable(int port, string? address = null);

    /// <summary>Tests basic DNS resolution.</summary>
    Task<bool> TestDnsResolutionAsync(string hostname = "dns.google", CancellationToken cancellationToken = default);

    /// <summary>Tests basic internet connectivity.</summary>
    Task<bool> TestInternetConnectivityAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Platform-specific path provider.
/// </summary>
public interface IPlatformPathProvider
{
    /// <summary>Configuration directory (e.g., C:\ProgramData\PrintPilotProxy\ or /etc/printpilotproxy/).</summary>
    string ConfigurationDirectory { get; }

    /// <summary>Log directory.</summary>
    string LogDirectory { get; }

    /// <summary>Data directory for runtime state.</summary>
    string DataDirectory { get; }

    /// <summary>Backup directory.</summary>
    string BackupDirectory { get; }

    /// <summary>Configuration file path.</summary>
    string ConfigurationFilePath { get; }

    /// <summary>Ensures all required directories exist.</summary>
    void EnsureDirectoriesExist();
}
