using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Manages loading, saving, validating, and backing up proxy configuration.
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads the current configuration from storage.
    /// </summary>
    Task<ProxyConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the configuration to storage.
    /// </summary>
    Task SaveAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration and returns any validation errors.
    /// </summary>
    IReadOnlyList<string> Validate(ProxyConfiguration configuration);

    /// <summary>
    /// Creates a backup of the current configuration.
    /// </summary>
    Task<string> BackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores configuration from a backup file.
    /// </summary>
    Task<ProxyConfiguration> RestoreAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available backup files.
    /// </summary>
    IReadOnlyList<string> GetBackups();

    /// <summary>
    /// Exports configuration to a specified path.
    /// </summary>
    Task ExportAsync(string exportPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports configuration from a specified path.
    /// </summary>
    Task<ProxyConfiguration> ImportAsync(string importPath, CancellationToken cancellationToken = default);
}
