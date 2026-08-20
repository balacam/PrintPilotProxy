using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.ViewModels;

/// <summary>
/// Displays and controls the actual Windows Service Control Manager state. The
/// proxy-engine status is shown separately because a running service can keep
/// the engine stopped for configuration or recovery reasons.
/// </summary>
public partial class ServiceViewModel : ObservableObject, IDisposable
{
    private readonly IpcClientService _ipc;
    private readonly IPlatformServiceManager _serviceManager;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _windowsServiceState = "Unknown";
    [ObservableProperty] private string _startupType = "Unknown";
    [ObservableProperty] private string _proxyStateText = "N/A";
    [ObservableProperty] private string _proxyStateBrushKey = "WarningBrush";
    [ObservableProperty] private string _listeningAddress = "N/A";
    [ObservableProperty] private string _uptime = "N/A";
    [ObservableProperty] private string _totalRequests = "N/A";
    [ObservableProperty] private string _totalErrors = "N/A";
    [ObservableProperty] private string _activeConnections = "N/A";
    [ObservableProperty] private bool _autoRestartOnFailure = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartServiceCommand))]
    private bool _canStart;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopServiceCommand))]
    private bool _canStop;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartServiceCommand))]
    private bool _canRestart;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartProxyEngineCommand))]
    private bool _canStartProxyEngine;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopProxyEngineCommand))]
    private bool _canStopProxyEngine;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartProxyEngineCommand))]
    private bool _canRestartProxyEngine;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    private bool _isRefreshing;

    public ServiceViewModel(IpcClientService ipc, IPlatformServiceManager serviceManager)
    {
        _ipc = ipc;
        _serviceManager = serviceManager;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_isRefreshing || IsBusy) return;
        _isRefreshing = true;

        try
        {
            var service = await _serviceManager.GetInfoAsync();
            WindowsServiceState = service.Status.ToString();
            StartupType = service.StartupType.ToString();
            CanStart = service.Status == ServiceStatus.Stopped;
            CanStop = service.Status is ServiceStatus.Running or ServiceStatus.Starting;
            CanRestart = service.Status == ServiceStatus.Running;

            if (service.Status != ServiceStatus.Running)
            {
                ProxyStateText = service.Status == ServiceStatus.NotInstalled ? LocalizationService.Instance["Svc.NotInstalled"] : LocalizationService.Instance["Common.Na"];
                ProxyStateBrushKey = service.Status == ServiceStatus.NotInstalled ? "ErrorBrush" : "WarningBrush";
                ListeningAddress = Uptime = TotalRequests = TotalErrors = ActiveConnections = "N/A";
                CanStartProxyEngine = false;
                CanStopProxyEngine = false;
                CanRestartProxyEngine = false;
                if (!string.IsNullOrWhiteSpace(service.ErrorMessage))
                {
                    StatusMessage = service.ErrorMessage;
                    StatusIsError = true;
                }
                return;
            }

            var status = await _ipc.GetStatusAsync();
            if (status is null)
            {
                ProxyStateText = LocalizationService.Instance["Svc.Unavailable"];
                ProxyStateBrushKey = "ErrorBrush";
                ListeningAddress = Uptime = TotalRequests = TotalErrors = ActiveConnections = "N/A";
                CanStartProxyEngine = false;
                CanStopProxyEngine = false;
                CanRestartProxyEngine = false;
                return;
            }

            ProxyStateText = status.State.ToString();
            ProxyStateBrushKey = BrushForState(status.State);
            
            CanStartProxyEngine = status.State == ProxyState.Stopped || status.State == ProxyState.Faulted;
            CanStopProxyEngine = status.State == ProxyState.Running;
            CanRestartProxyEngine = status.State == ProxyState.Running || status.State == ProxyState.Faulted;
            
            ListeningAddress = status.ListeningAddress ?? "N/A";
            TotalRequests = status.TotalRequests.ToString("N0");
            TotalErrors = status.TotalErrors.ToString("N0");
            ActiveConnections = status.ActiveConnections.ToString("N0");
            Uptime = FormatUptime(status.Uptime);

            var configuration = await _ipc.GetConfigurationAsync();
            if (configuration is not null)
            {
                AutoRestartOnFailure = configuration.Service.AutoRestartOnFailure;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            StatusIsError = true;
        }
        finally
        {
            _isRefreshing = false;
            StartServiceCommand.NotifyCanExecuteChanged();
            StopServiceCommand.NotifyCanExecuteChanged();
            RestartServiceCommand.NotifyCanExecuteChanged();
            StartProxyEngineCommand.NotifyCanExecuteChanged();
            StopProxyEngineCommand.NotifyCanExecuteChanged();
            RestartProxyEngineCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartServiceAsync() => ExecuteServiceCommandAsync(_serviceManager.StartAsync, LocalizationService.Instance["Svc.Msgs.Started"]);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopServiceAsync() => ExecuteServiceCommandAsync(_serviceManager.StopAsync, LocalizationService.Instance["Svc.Msgs.Stopped"]);

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private Task RestartServiceAsync() => ExecuteServiceCommandAsync(_serviceManager.RestartAsync, LocalizationService.Instance["Svc.Msgs.Restarted"]);

    [RelayCommand(CanExecute = nameof(CanStartProxyEngine))]
    private async Task StartProxyEngineAsync()
    {
        IsBusy = true;
        var (success, msg) = await _ipc.StartProxyAsync();
        StatusMessage = success ? LocalizationService.Instance["Svc.Msgs.EngineStarted"] : msg;
        StatusIsError = !success;
        IsBusy = false;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStopProxyEngine))]
    private async Task StopProxyEngineAsync()
    {
        IsBusy = true;
        var (success, msg) = await _ipc.StopProxyAsync();
        StatusMessage = success ? LocalizationService.Instance["Svc.Msgs.EngineStopped"] : msg;
        StatusIsError = !success;
        IsBusy = false;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRestartProxyEngine))]
    private async Task RestartProxyEngineAsync()
    {
        IsBusy = true;
        var (success, msg) = await _ipc.RestartProxyAsync();
        StatusMessage = success ? LocalizationService.Instance["Svc.Msgs.EngineRestarted"] : msg;
        StatusIsError = !success;
        IsBusy = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UpdateAutoRestartAsync()
    {
        try
        {
            IsBusy = true;
            var configuration = await _ipc.GetConfigurationAsync();
            if (configuration is null)
            {
                throw new InvalidOperationException(LocalizationService.Instance["Svc.Msgs.ServiceRequired"]);
            }

            configuration.Service.AutoRestartOnFailure = AutoRestartOnFailure;
            configuration.LastModified = DateTimeOffset.UtcNow;
            var (success, message) = await _ipc.UpdateConfigurationAsync(configuration);
            StatusMessage = success ? LocalizationService.Instance["Svc.Msgs.RecoveryUpdated"] : message;
            StatusIsError = !success;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            StatusIsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteServiceCommandAsync(
        Func<CancellationToken, Task<bool>> command,
        string successMessage)
    {
        try
        {
            IsBusy = true;
            CanStart = CanStop = CanRestart = false;
            var ok = await command(CancellationToken.None);
            StatusMessage = ok ? successMessage : LocalizationService.Instance["Svc.Msgs.CommandFailed"];
            StatusIsError = !ok;

            if (ok)
            {
                // Wait for the Windows Service IPC endpoint to start responding (up to 8s)
                for (int i = 0; i < 16; i++)
                {
                    await Task.Delay(500);
                    var probe = await _ipc.GetStatusAsync();
                    if (probe != null)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            StatusIsError = true;
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    private static string BrushForState(ProxyState state) => state switch
    {
        ProxyState.Running => "SuccessBrush",
        ProxyState.Stopped or ProxyState.Faulted => "ErrorBrush",
        _ => "WarningBrush"
    };

    private static string FormatUptime(TimeSpan? uptime)
        => uptime.HasValue
            ? $"{(int)uptime.Value.TotalHours:D2}:{uptime.Value.Minutes:D2}:{uptime.Value.Seconds:D2}"
            : "N/A";

    public void Dispose()
    {
        _timer?.Stop();
    }
}
