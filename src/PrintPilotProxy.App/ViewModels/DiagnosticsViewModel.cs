using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.App.ViewModels;

// Unified check result — avoids overload-resolution issues with value-tuple lambdas
internal sealed class CheckResult
{
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Remediation { get; init; }

    public static CheckResult Ok(string msg)  => new() { Passed = true,  Message = msg };
    public static CheckResult Fail(string msg, string? fix = null)
        => new() { Passed = false, Message = msg, Remediation = fix };
}

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    public ObservableCollection<DiagnosticItem> Results { get; } = new();

    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _overallStatus = string.Empty;
    [ObservableProperty] private string _overallStatusBrushKey = "TextBrush";

    public DiagnosticsViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        _ = RunAllAsync();
    }

    [RelayCommand]
    private async Task RunAllAsync()
    {
        try
        {
            IsBusy = true;
            Results.Clear();
            OverallStatus = LocalizationService.Instance["Diag.Running"];
            OverallStatusBrushKey = "WarningBrush";

            await RunCheck(LocalizationService.Instance["Diag.ServiceConnection"],
                LocalizationService.Instance["Diag.ServiceConnectionDesc"],
                async () =>
                {
                    var s = await _ipc.GetStatusAsync();
                    return s != null
                        ? CheckResult.Ok(LocalizationService.Instance["Diag.Connected"])
                        : CheckResult.Fail(LocalizationService.Instance["Diag.CannotConnect"],
                            LocalizationService.Instance["Diag.StartServiceHint"]);
                });

            await RunCheck(LocalizationService.Instance["Diag.ConfigValid"],
                LocalizationService.Instance["Diag.ConfigValidDesc"],
                async () =>
                {
                    var cfg = await _ipc.GetConfigurationAsync();
                    if (cfg == null)
                        return CheckResult.Fail(LocalizationService.Instance["Diag.CouldNotLoadConfig"],
                            LocalizationService.Instance["Diag.EnsureServiceRunning"]);
                    var errors = ConfigurationValidator.Validate(cfg);
                    return errors.Count == 0
                        ? CheckResult.Ok(LocalizationService.Instance["Diag.ConfigValidOk"])
                        : CheckResult.Fail(LocalizationService.Instance.GetFormat("Diag.ConfigErrors", errors.Count, string.Join("; ", errors)),
                            LocalizationService.Instance["Diag.FixConfigErrors"]);
                });

            await RunCheck(LocalizationService.Instance["Diag.ProxyEngineState"],
                LocalizationService.Instance["Diag.ProxyEngineStateDesc"],
                async () =>
                {
                    var s = await _ipc.GetStatusAsync();
                    if (s == null)
                        return CheckResult.Fail(LocalizationService.Instance["Diag.ServiceNotReachable"], LocalizationService.Instance["Diag.StartService"]);
                    return s.State == ProxyState.Running
                        ? CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.RunningOn", s.ListeningAddress))
                        : CheckResult.Fail(LocalizationService.Instance.GetFormat("Diag.StateIs", s.State), LocalizationService.Instance["Diag.UseServicePage"]);
                });

            await RunCheck(LocalizationService.Instance["Diag.ConfigFileDisk"],
                LocalizationService.Instance["Diag.ConfigFileDiskDesc"],
                () =>
                {
                    var path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "PrintPilotProxy", "config.json");
                    return Task.FromResult(File.Exists(path)
                        ? CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.FoundPath", path))
                        : CheckResult.Fail(LocalizationService.Instance.GetFormat("Diag.NotFoundPath", path),
                            LocalizationService.Instance["Diag.RunInstaller"]));
                });

            await RunCheck(LocalizationService.Instance["Diag.LogDirWritable"],
                LocalizationService.Instance["Diag.LogDirWritableDesc"],
                () =>
                {
                    var logDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "PrintPilotProxy", "logs");
                    if (!Directory.Exists(logDir))
                        return Task.FromResult(CheckResult.Fail(LocalizationService.Instance.GetFormat("Diag.LogDirMissing", logDir),
                            LocalizationService.Instance["Diag.StartServiceCreatesDirs"]));
                    try
                    {
                        var tmp = System.IO.Path.Combine(logDir, $".test_{Guid.NewGuid():N}");
                        File.WriteAllText(tmp, "test");
                        File.Delete(tmp);
                        return Task.FromResult(CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.LogDirWritableOk", logDir)));
                    }
                    catch
                    {
                        return Task.FromResult(CheckResult.Fail(LocalizationService.Instance["Diag.LogDirNotWritable"],
                            LocalizationService.Instance["Diag.CheckPermissions"]));
                    }
                });

            await RunCheck(LocalizationService.Instance["Diag.PortAvailability"],
                LocalizationService.Instance["Diag.PortAvailabilityDesc"],
                async () =>
                {
                    var cfg = await _ipc.GetConfigurationAsync();
                    if (cfg == null) return CheckResult.Fail(LocalizationService.Instance["Diag.CouldNotLoadConfigShort"]);
                    var port = cfg.Listener.Port;

                    var status = await _ipc.GetStatusAsync();
                    if (status?.State == ProxyState.Running)
                        return CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.PortBound", port));

                    try
                    {
                        using var l = new TcpListener(System.Net.IPAddress.Loopback, port);
                        l.Start(); l.Stop();
                        return CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.PortAvailable", port));
                    }
                    catch
                    {
                        return CheckResult.Fail(LocalizationService.Instance.GetFormat("Diag.PortInUse", port),
                            LocalizationService.Instance["Diag.ChangePortHint"]);
                    }
                });

            await RunCheck(LocalizationService.Instance["Diag.NetworkAdapters"],
                LocalizationService.Instance["Diag.NetworkAdaptersDesc"],
                async () =>
                {
                    var ifaces = await _ipc.GetNetworkInterfacesAsync();
                    return ifaces.Count > 0
                        ? CheckResult.Ok(LocalizationService.Instance.GetFormat("Diag.AdaptersDetected", ifaces.Count))
                        : CheckResult.Fail(LocalizationService.Instance["Diag.NoAdapters"], LocalizationService.Instance["Diag.CheckNetworkConfig"]);
                });

            await RunCheck(LocalizationService.Instance["Diag.FirewallRule"],
                LocalizationService.Instance["Diag.FirewallRuleDesc"],
                async () =>
                {
                    var fw = await _ipc.GetFirewallStatusAsync();
                    if (fw == null)
                        return CheckResult.Fail(LocalizationService.Instance["Diag.CouldNotQueryFirewall"],
                            LocalizationService.Instance["Diag.EnsureServiceRunning"]);
                    return fw.RuleExists
                        ? CheckResult.Ok(LocalizationService.Instance["Diag.FirewallRuleExists"])
                        : CheckResult.Fail(LocalizationService.Instance["Diag.FirewallRuleMissing"],
                            LocalizationService.Instance["Diag.CreateRuleHint"]);
                });

            await RunCheck(LocalizationService.Instance["Diag.IpcPipe"],
                LocalizationService.Instance["Diag.IpcPipeDesc"],
                async () =>
                {
                    var s = await _ipc.GetStatusAsync();
                    return s != null
                        ? CheckResult.Ok(LocalizationService.Instance["Diag.IpcResponding"])
                        : CheckResult.Fail(LocalizationService.Instance["Diag.IpcNotResponding"],
                            LocalizationService.Instance["Diag.RestartService"]);
                });

            int passed = 0, failed = 0;
            foreach (var r in Results) { if (r.Passed) passed++; else failed++; }
            OverallStatus = failed == 0
                ? LocalizationService.Instance.GetFormat("Diag.AllPassed", passed)
                : LocalizationService.Instance.GetFormat("Diag.PassedFailed", passed, failed);
            OverallStatusBrushKey = failed == 0 ? "SuccessBrush" : "ErrorBrush";
        }
        catch (Exception ex)
        {
            OverallStatus = LocalizationService.Instance.GetFormat("Diag.RunError", ex.Message);
            OverallStatusBrushKey = "ErrorBrush";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunCheck(string name, string description,
        Func<Task<CheckResult>> check)
    {
        var item = new DiagnosticItem { Name = name, Description = description, IsRunning = true };
        Results.Add(item);
        var start = DateTime.UtcNow;
        try
        {
            var r = await check();
            item.Passed      = r.Passed;
            item.Message     = r.Message;
            item.Remediation = r.Remediation ?? string.Empty;
        }
        catch (Exception ex)
        {
            item.Passed      = false;
            item.Message     = LocalizationService.Instance.GetFormat("Diag.Exception", ex.Message);
            item.Remediation = LocalizationService.Instance["Diag.CheckLogs"];
        }
        finally
        {
            item.Duration  = $"{(DateTime.UtcNow - start).TotalMilliseconds:F0} ms";
            item.IsRunning = false;
        }
    }
}

public partial class DiagnosticItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool   _passed = false;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _remediation = string.Empty;
    [ObservableProperty] private string _duration = string.Empty;
    [ObservableProperty] private bool   _isRunning = false;

    public string StatusIcon      => IsRunning ? "⏳" : (Passed ? "✔" : "✘");
    public string StatusBrushKey  => IsRunning ? "WarningBrush" : (Passed ? "SuccessBrush" : "ErrorBrush");
}
