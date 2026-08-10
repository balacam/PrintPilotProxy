using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>Windows Service Control Manager implementation for PrintPilotProxy.</summary>
public sealed class WindowsServiceManager : IPlatformServiceManager
{
    public const string ServiceName = "PrintPilotProxy";
    public const string DisplayName = "PrintPilotProxy Forward Proxy Service";
    private static readonly Regex StatePattern = new(@"STATE\s*:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly ILogger<WindowsServiceManager> _logger;

    public WindowsServiceManager(ILogger<WindowsServiceManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsServiceManager>.Instance;
    }

    public async Task<bool> InstallServiceAsync(CancellationToken cancellationToken = default)
    {
        var serviceExecutable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PrintPilotProxy", "PrintPilotProxy.Service.exe");

        if (!File.Exists(serviceExecutable))
        {
            throw new FileNotFoundException("The installed PrintPilotProxy service executable was not found.", serviceExecutable);
        }

        var info = await GetInfoAsync(cancellationToken);
        if (info.Status != ServiceStatus.NotInstalled)
        {
            return true;
        }

        await RunScAsync($"create {ServiceName} binPath= \"{serviceExecutable}\" DisplayName= \"{DisplayName}\" start= auto", cancellationToken);
        await RunScAsync($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/120000/restart/300000", cancellationToken);
        await RunScAsync($"failureflag {ServiceName} 1", cancellationToken);
        _logger.LogInformation("Installed Windows Service {ServiceName}.", ServiceName);
        return true;
    }

    public async Task<bool> UninstallServiceAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status == ServiceStatus.NotInstalled)
        {
            return true;
        }

        if (info.Status is ServiceStatus.Running or ServiceStatus.Starting or ServiceStatus.Paused)
        {
            await StopAsync(cancellationToken);
        }

        await RunScAsync($"delete {ServiceName}", cancellationToken);
        _logger.LogInformation("Removed Windows Service {ServiceName}.", ServiceName);
        return true;
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => (await GetInfoAsync(cancellationToken)).Status;

    public async Task<PlatformServiceInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunScAsync($"query {ServiceName}", cancellationToken, throwOnError: false);
            if (result.ExitCode == 1060)
            {
                return new PlatformServiceInfo { Status = ServiceStatus.NotInstalled };
            }

            if (result.ExitCode != 0)
            {
                return new PlatformServiceInfo
                {
                    Status = ServiceStatus.Unknown,
                    ErrorMessage = CombineOutput(result)
                };
            }

            return new PlatformServiceInfo
            {
                Status = ParseStatus(result.StandardOutput),
                StartupType = ReadStartupType()
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query Windows Service {ServiceName}.", ServiceName);
            return new PlatformServiceInfo { Status = ServiceStatus.Unknown, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status == ServiceStatus.NotInstalled)
        {
            throw new InvalidOperationException("PrintPilotProxy is not installed as a Windows Service.");
        }

        if (info.Status == ServiceStatus.Running)
        {
            return true;
        }

        var result = await RunScAsync($"start {ServiceName}", cancellationToken, throwOnError: false);
        if (result.ExitCode is not 0 and not 1056)
        {
            throw new InvalidOperationException($"Could not start the PrintPilotProxy service: {CombineOutput(result)}");
        }

        await WaitForStateAsync(new[] { ServiceStatus.Running }, cancellationToken);
        _logger.LogInformation("Started Windows Service {ServiceName}.", ServiceName);
        return true;
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        if (info.Status is ServiceStatus.NotInstalled or ServiceStatus.Stopped)
        {
            return true;
        }

        var result = await RunScAsync($"stop {ServiceName}", cancellationToken, throwOnError: false);
        if (result.ExitCode is not 0 and not 1062)
        {
            throw new InvalidOperationException($"Could not stop the PrintPilotProxy service: {CombineOutput(result)}");
        }

        await WaitForStateAsync(new[] { ServiceStatus.Stopped, ServiceStatus.NotInstalled }, cancellationToken);
        _logger.LogInformation("Stopped Windows Service {ServiceName}.", ServiceName);
        return true;
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        return await StartAsync(cancellationToken);
    }

    private async Task WaitForStateAsync(IReadOnlyCollection<ServiceStatus> desiredStates, CancellationToken cancellationToken)
    {
        const int attempts = 20;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var state = await GetStatusAsync(cancellationToken);
            if (desiredStates.Contains(state))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        throw new TimeoutException("The PrintPilotProxy service did not reach the requested state in time.");
    }

    private static ServiceStatus ParseStatus(string output)
    {
        var match = StatePattern.Match(output);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var state))
        {
            return ServiceStatus.Unknown;
        }

        return state switch
        {
            1 => ServiceStatus.Stopped,
            2 => ServiceStatus.Starting,
            3 => ServiceStatus.Stopping,
            4 => ServiceStatus.Running,
            7 => ServiceStatus.Paused,
            _ => ServiceStatus.Unknown
        };
    }

    private static ServiceStartupType ReadStartupType()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ServiceStartupType.Unknown;
        }

        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
        if (key is null)
        {
            return ServiceStartupType.Unknown;
        }

        var start = key.GetValue("Start") is int startValue ? startValue : (int?)null;
        if (start == 2 && key.GetValue("DelayedAutoStart") is int delayed && delayed != 0)
        {
            return ServiceStartupType.AutomaticDelayed;
        }

        return start switch
        {
            2 => ServiceStartupType.Automatic,
            3 => ServiceStartupType.Manual,
            4 => ServiceStartupType.Disabled,
            _ => ServiceStartupType.Unknown
        };
    }

    private static async Task<ScResult> RunScAsync(string arguments, CancellationToken cancellationToken, bool throwOnError = true)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var result = new ScResult(process.ExitCode, await standardOutputTask, await standardErrorTask);

        if (result.ExitCode == 5) // Access Denied -> Elevate with UAC prompt
        {
            try
            {
                using var elevatedProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
                if (elevatedProcess != null)
                {
                    await elevatedProcess.WaitForExitAsync(cancellationToken);
                    return new ScResult(elevatedProcess.ExitCode, "Elevated command completed.", string.Empty);
                }
            }
            catch { /* User declined UAC prompt */ }
        }

        if (throwOnError && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"sc.exe exited with code {result.ExitCode}: {CombineOutput(result)}");
        }

        return result;
    }

    private static string CombineOutput(ScResult result)
        => $"{result.StandardError} {result.StandardOutput}".Trim();

    private sealed record ScResult(int ExitCode, string StandardOutput, string StandardError);
}
