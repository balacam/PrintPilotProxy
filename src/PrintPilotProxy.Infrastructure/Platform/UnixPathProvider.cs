using System;
using System.IO;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// Path provider for Linux and macOS. Linux follows the filesystem hierarchy standard
/// (<c>/etc</c>, <c>/var/log</c>, <c>/var/lib</c>); macOS uses <c>/Library</c>. The directories can be moved with
/// the <c>PRINTPILOTPROXY_ROOT</c> environment variable (used by tests and unprivileged development runs).
/// </summary>
public sealed class UnixPathProvider : IPlatformPathProvider
{
    public const string RootOverrideVariable = "PRINTPILOTPROXY_ROOT";

    private readonly string _configurationDirectory;
    private readonly string _logDirectory;
    private readonly string _dataDirectory;

    public UnixPathProvider()
    {
        var root = Environment.GetEnvironmentVariable(RootOverrideVariable);
        if (!string.IsNullOrWhiteSpace(root))
        {
            _configurationDirectory = Path.Combine(root, "etc");
            _logDirectory = Path.Combine(root, "logs");
            _dataDirectory = Path.Combine(root, "data");
        }
        else if (OperatingSystem.IsMacOS())
        {
            const string support = "/Library/Application Support/PrintPilotProxy";
            _configurationDirectory = support;
            _logDirectory = "/Library/Logs/PrintPilotProxy";
            _dataDirectory = Path.Combine(support, "data");
        }
        else
        {
            _configurationDirectory = "/etc/printpilotproxy";
            _logDirectory = "/var/log/printpilotproxy";
            _dataDirectory = "/var/lib/printpilotproxy";
        }
    }

    public string ConfigurationDirectory => _configurationDirectory;
    public string LogDirectory => _logDirectory;
    public string DataDirectory => _dataDirectory;
    public string BackupDirectory => Path.Combine(_dataDirectory, "backups");
    public string ConfigurationFilePath => Path.Combine(_configurationDirectory, "config.json");

    public void EnsureDirectoriesExist()
    {
        foreach (var directory in new[] { ConfigurationDirectory, LogDirectory, DataDirectory, BackupDirectory })
        {
            Directory.CreateDirectory(directory);
        }

        // The configuration holds the protected upstream password and the data folder holds the key that protects it.
        PrivateFileAcl.Apply(BackupDirectory);
        PrivateFileAcl.Apply(DataDirectory);
        PrivateFileAcl.Apply(ConfigurationFilePath);
    }
}

/// <summary>Chooses the platform implementations; shared by the service host and the CLI.</summary>
public static class PlatformPaths
{
    public static IPlatformPathProvider CreateProvider()
        => OperatingSystem.IsWindows() ? new WindowsPathProvider() : new UnixPathProvider();
}
