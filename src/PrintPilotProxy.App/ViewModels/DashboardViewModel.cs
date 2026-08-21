using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IpcClientService _ipc;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _proxyState = "N/A";
    [ObservableProperty] private string _proxyStateRaw = "Stopped";
    [ObservableProperty] private string _proxyStateBrushKey = "WarningBrush";
    [ObservableProperty] private string _engineName = "N/A";
    [ObservableProperty] private string _engineVersion = "N/A";
    [ObservableProperty] private string _listeningAddress = "N/A";
    [ObservableProperty] private string _proxyPort = "N/A";
    [ObservableProperty] private string _uptimeString = "N/A";
    [ObservableProperty] private string _totalRequests = "N/A";
    [ObservableProperty] private string _totalErrors = "N/A";
    [ObservableProperty] private string _activeConnections = "N/A";
    [ObservableProperty] private string _accessMode = "N/A";
    [ObservableProperty] private string _allowedClientsCount = "N/A";
    [ObservableProperty] private string _allowedPorts = "N/A";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<ActivityLogEntry> RecentActivities { get; } = new();

    private bool _isRefreshing;

    public DashboardViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        LocalizationService.Instance.PropertyChanged += (_, _) => _ = RefreshAsync();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            IsLoading = true;
            StatusMessage = string.Empty;

            var status = await _ipc.GetStatusAsync();
            if (status != null)
            {
                ProxyStateRaw = status.State.ToString();
                ProxyState = LogLocalizer.Localize(status.State.ToString());
                ProxyStateBrushKey = BrushForState(status.State);
                EngineName = status.EngineName;
                EngineVersion = status.EngineVersion;
                ListeningAddress = status.ListeningAddress ?? "N/A";
                ProxyPort = ExtractPort(status.ListeningAddress);
                TotalRequests = status.TotalRequests.ToString("N0");
                TotalErrors = status.TotalErrors.ToString("N0");
                ActiveConnections = status.ActiveConnections.ToString("N0");
                UptimeString = FormatUptime(status.Uptime);
            }
            else
            {
                ProxyStateRaw = "Stopped";
                ProxyState = LocalizationService.Instance["Dash.ServiceUnavailable"];
                ProxyStateBrushKey = "ErrorBrush";
                ListeningAddress = TotalRequests = TotalErrors = ActiveConnections = UptimeString = "N/A";
            }

            var config = await _ipc.GetConfigurationAsync();
            if (config != null)
            {
                AccessMode = config.ClientAccess.Mode == ClientAccessMode.AllowAll 
                    ? LocalizationService.Instance["Net.ClientAccessAllowAll"]
                    : LocalizationService.Instance["Net.ClientAccessAllowList"];
                AllowedClientsCount = config.ClientAccess.AllowedClients.Count.ToString();
                AllowedPorts = config.Security.DestinationPortRestrictionsEnabled
                    ? string.Join(", ", config.Security.AllowedDestinationPorts)
                    : LocalizationService.Instance["Common.AllPorts"];
            }

            var requests = await _ipc.GetRecentRequestsAsync();
            RecentActivities.Clear();
            foreach (var r in requests)
            {
                RecentActivities.Add(new ActivityLogEntry
                {
                    Time        = r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    ClientIp    = r.ClientIp,
                    Method      = r.Method,
                    Destination = r.Destination,
                    Status      = r.IsSuccess 
                        ? LocalizationService.Instance["Common.OK"] 
                        : (!string.IsNullOrEmpty(r.ErrorMessage) ? LogLocalizer.Localize(r.ErrorMessage) : LocalizationService.Instance["Common.Error"]),
                    StatusBrushKey = r.IsSuccess ? "SuccessBrush" : "ErrorBrush"
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Instance.GetFormat("Dash.Msgs.LoadError", ex.Message);
        }
        finally
        {
            IsLoading = false;
            _isRefreshing = false;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string BrushForState(PrintPilotProxy.Core.Models.ProxyState state) => state switch
    {
        PrintPilotProxy.Core.Models.ProxyState.Running  => "SuccessBrush",
        PrintPilotProxy.Core.Models.ProxyState.Stopped  => "ErrorBrush",
        PrintPilotProxy.Core.Models.ProxyState.Faulted  => "ErrorBrush",
        _                                               => "WarningBrush"
    };

    private static string ExtractPort(string? address)
    {
        if (string.IsNullOrEmpty(address)) return "N/A";
        var idx = address.LastIndexOf(':');
        return idx >= 0 ? address[(idx + 1)..] : "N/A";
    }

    private static string FormatUptime(TimeSpan? uptime)
    {
        if (!uptime.HasValue) return "N/A";
        var u = uptime.Value;
        return $"{(int)u.TotalHours:D2}:{u.Minutes:D2}:{u.Seconds:D2}";
    }

    public void Dispose()
    {
        _timer?.Stop();
    }
}

public class ActivityLogEntry
{
    public string Time { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusBrushKey { get; set; } = "TextBrush";
}
