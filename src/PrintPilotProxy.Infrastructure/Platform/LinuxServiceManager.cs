using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// systemd implementation. The unit file itself is installed by the package (<c>deploy/linux</c>); this class
/// only controls and inspects it through <c>systemctl</c>, so it never writes into <c>/etc/systemd</c>.
/// </summary>
public sealed class LinuxServiceManager : IPlatformServiceManager
{
    public const string UnitName = "printpilotproxy.service";
    private readonly ILogger<LinuxServiceManager> _logger;

    public LinuxServiceManager(ILogger<LinuxServiceManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinuxServiceManager>.Instance;
    }

    public async Task<bool> InstallServiceAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status == ServiceStatus.NotInstalled)
        {
            throw new InvalidOperationException(
                "The systemd unit is not installed. Install the PrintPilotProxy package or copy deploy/linux/printpilotproxy.service to /etc/systemd/system/.");
        }

        await RunAsync($"enable {UnitName}", cancellationToken, throwOnError: true);
        return true;
    }

    public async Task<bool> UninstallServiceAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status == ServiceStatus.NotInstalled)
        {
            return true;
        }

        await RunAsync($"disable --now {UnitName}", cancellationToken, throwOnError: true);
        return true;
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => (await GetInfoAsync(cancellationToken)).Status;

    public async Task<PlatformServiceInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var show = await RunAsync(
                $"show {UnitName} --property=LoadState,ActiveState,UnitFileState --no-pager",
                cancellationToken, throwOnError: false);
            if (show.ExitCode != 0)
            {
                return new PlatformServiceInfo { Status = ServiceStatus.Unknown, ErrorMessage = show.Output };
            }

            var properties = ParseProperties(show.Output);
            properties.TryGetValue("LoadState", out var load);
            if (string.Equals(load, "not-found", StringComparison.OrdinalIgnoreCase))
            {
                return new PlatformServiceInfo { Status = ServiceStatus.NotInstalled };
            }

            properties.TryGetValue("ActiveState", out var active);
            properties.TryGetValue("UnitFileState", out var unitFile);
            return new PlatformServiceInfo { Status = ParseStatus(active), StartupType = ParseStartup(unitFile) };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query systemd unit {Unit}.", UnitName);
            return new PlatformServiceInfo { Status = ServiceStatus.Unknown, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        => await ControlAsync("start", cancellationToken);

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        => await ControlAsync("stop", cancellationToken);

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
        => await ControlAsync("restart", cancellationToken);

    internal static ServiceStatus ParseStatus(string? activeState) => activeState?.Trim().ToLowerInvariant() switch
    {
        "active" or "reloading" => ServiceStatus.Running,
        "activating" => ServiceStatus.Starting,
        "deactivating" => ServiceStatus.Stopping,
        "inactive" or "failed" => ServiceStatus.Stopped,
        _ => ServiceStatus.Unknown
    };

    internal static ServiceStartupType ParseStartup(string? unitFileState) => unitFileState?.Trim().ToLowerInvariant() switch
    {
        "enabled" or "enabled-runtime" or "static" or "alias" => ServiceStartupType.Automatic,
        "disabled" => ServiceStartupType.Disabled,
        "masked" or "masked-runtime" => ServiceStartupType.Disabled,
        _ => ServiceStartupType.Unknown
    };

    internal static Dictionary<string, string> ParseProperties(string output)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return result;
    }

    private async Task<bool> ControlAsync(string verb, CancellationToken cancellationToken)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status == ServiceStatus.NotInstalled)
        {
            throw new InvalidOperationException("PrintPilotProxy is not installed as a systemd service.");
        }

        await RunAsync($"{verb} {UnitName}", cancellationToken, throwOnError: true);
        _logger.LogInformation("systemctl {Verb} {Unit} succeeded.", verb, UnitName);
        return true;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string arguments, CancellationToken cancellationToken, bool throwOnError)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("systemctl", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await stdout + await stderr).Trim();
        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"systemctl {arguments} failed ({process.ExitCode}): {output}");
        }

        return (process.ExitCode, output);
    }
}
