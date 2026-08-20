using System;
using System.IO;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// Thread-safe provider for the persistent proxy instance ID.
/// Stored in the data directory and persists across service restarts.
/// </summary>
public sealed class PersistentProxyInstanceProvider : IProxyInstanceProvider
{
    private readonly IPlatformPathProvider _pathProvider;
    private readonly ILogger<PersistentProxyInstanceProvider> _logger;
    private readonly object _lock = new();
    private string? _cachedInstanceId;

    public PersistentProxyInstanceProvider(
        IPlatformPathProvider pathProvider,
        ILogger<PersistentProxyInstanceProvider>? logger = null)
    {
        _pathProvider = pathProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PersistentProxyInstanceProvider>.Instance;
    }

    public string GetInstanceId()
    {
        if (_cachedInstanceId != null)
        {
            return _cachedInstanceId;
        }

        lock (_lock)
        {
            if (_cachedInstanceId != null)
            {
                return _cachedInstanceId;
            }

            _cachedInstanceId = LoadOrCreateInstanceId();
            return _cachedInstanceId;
        }
    }

    private string LoadOrCreateInstanceId()
    {
        try
        {
            _pathProvider.EnsureDirectoriesExist();
            var instanceFilePath = Path.Combine(_pathProvider.DataDirectory, "instance.id");

            if (File.Exists(instanceFilePath))
            {
                var existingId = File.ReadAllText(instanceFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(existingId) && Guid.TryParse(existingId, out _))
                {
                    _logger.LogDebug("Loaded existing PrintPilotProxy instance ID: {InstanceId}", existingId);
                    return existingId;
                }
            }

            var newInstanceId = Guid.NewGuid().ToString("D");
            var tempPath = instanceFilePath + ".tmp";

            File.WriteAllText(tempPath, newInstanceId);
            File.Move(tempPath, instanceFilePath, overwrite: true);

            _logger.LogInformation("Generated and saved new PrintPilotProxy instance ID: {InstanceId}", newInstanceId);
            return newInstanceId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist instance ID to file system. Generating ephemeral fallback.");
            return Guid.NewGuid().ToString("D");
        }
    }
}
