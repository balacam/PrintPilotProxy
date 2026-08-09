using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform
{
    public class WindowsServiceManager : IPlatformServiceManager
    {
        private readonly ILogger<WindowsServiceManager> _logger;
        private const string ServiceName = "PrintPilotProxy";
        private const string DisplayName = "PrintPilotProxy Forward Proxy Service";

        public WindowsServiceManager(ILogger<WindowsServiceManager> logger)
        {
            _logger = logger;
        }

        public async Task<bool> InstallServiceAsync(CancellationToken cancellationToken = default)
        {
            string binPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            try
            {
                string args = $"create {ServiceName} binPath= \"{binPath}\" DisplayName= \"{DisplayName}\" start= auto";
                await RunScAsync(args, cancellationToken);
                _logger.LogInformation("Service installed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install service.");
                throw;
            }
        }

        public async Task<bool> UninstallServiceAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string args = $"delete {ServiceName}";
                await RunScAsync(args, cancellationToken);
                _logger.LogInformation("Service uninstalled successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to uninstall service.");
                throw;
            }
        }

        public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string args = $"query {ServiceName}";
                var output = await RunScWithOutputAsync(args, cancellationToken);
                
                string state = "Unknown";
                if (output.Contains("RUNNING")) state = "Running";
                else if (output.Contains("STOPPED")) state = "Stopped";
                else if (output.Contains("PAUSED")) state = "Paused";

                if (state == "Running") return ServiceStatus.Running;
                if (state == "Stopped") return ServiceStatus.Stopped;
                if (state == "Paused") return ServiceStatus.Paused;
                return ServiceStatus.Unknown;
            }
            catch (Exception)
            {
                return ServiceStatus.NotInstalled;
            }
        }

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string args = $"start {ServiceName}";
                await RunScAsync(args, cancellationToken);
                _logger.LogInformation("Service started.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start service.");
                throw;
            }
        }

        public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string args = $"stop {ServiceName}";
                await RunScAsync(args, cancellationToken);
                _logger.LogInformation("Service stopped.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop service.");
                throw;
            }
        }

        public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
        {
            await StopAsync(cancellationToken);
            await Task.Delay(1000, cancellationToken); // Give it a moment to stop
            return await StartAsync(cancellationToken);
        }

        private Task RunScAsync(string arguments, CancellationToken cancellationToken)
        {
            return RunScWithOutputAsync(arguments, cancellationToken);
        }

        private async Task<string> RunScWithOutputAsync(string arguments, CancellationToken cancellationToken)
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
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 && !error.Contains("The specified service does not exist as an installed service"))
            {
                throw new InvalidOperationException($"sc error: {error}. Output: {output}");
            }

            return output;
        }
    }
}
