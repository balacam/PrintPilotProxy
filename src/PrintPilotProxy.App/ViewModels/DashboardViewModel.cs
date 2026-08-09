using CommunityToolkit.Mvvm.ComponentModel;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PrintPilotProxy.App.ViewModels;

public class ActivityLog
{
    public string Time { get; set; } = "";
    public string ClientIp { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Status { get; set; } = "";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IIpcClient _ipcClient;
    private DispatcherTimer _timer;

    [ObservableProperty]
    private string _engineName = "Unknown";

    [ObservableProperty]
    private int _allowedClientsCount = 0;

    [ObservableProperty]
    private int _totalRequests = 0;

    [ObservableProperty]
    private int _totalErrors = 0;

    [ObservableProperty]
    private string _uptimeString = "00:00:00";

    public ObservableCollection<ActivityLog> RecentActivities { get; } = new();

    public DashboardViewModel(IIpcClient ipcClient)
    {
        _ipcClient = ipcClient;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (s, e) => await UpdateDashboardAsync();
        _timer.Start();
    }

    private async Task UpdateDashboardAsync()
    {
        try
        {
            if (!_ipcClient.IsConnected)
            {
                await _ipcClient.ConnectAsync();
            }

            // Get Status
            var statusReq = new IpcMessage { Type = IpcMessageTypes.GetStatus };
            var statusResp = await _ipcClient.SendAsync(statusReq);

            if (statusResp.Type == IpcMessageTypes.StatusResponse && statusResp.Payload != null)
            {
                var status = JsonSerializer.Deserialize<ProxyStatus>(statusResp.Payload);
                if (status != null)
                {
                    EngineName = $"{status.EngineName} {status.EngineVersion}".Trim();
                    TotalRequests = (int)status.TotalRequests;
                    TotalErrors = (int)status.TotalErrors;
                    
                    if (status.Uptime.HasValue)
                    {
                        var uptime = status.Uptime.Value;
                        UptimeString = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
                    }
                    else
                    {
                        UptimeString = "00:00:00";
                    }
                }
            }

            // Get recent requests
            var logsReq = new IpcMessage { Type = IpcMessageTypes.GetRecentRequests };
            var logsResp = await _ipcClient.SendAsync(logsReq);
            if (logsResp.Type == IpcMessageTypes.RecentRequestsResponse && logsResp.Payload != null)
            {
                var logs = JsonSerializer.Deserialize<ProxyRequestEntry[]>(logsResp.Payload);
                if (logs != null)
                {
                    RecentActivities.Clear();
                    foreach (var log in logs)
                    {
                        RecentActivities.Add(new ActivityLog
                        {
                            Time = log.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                            ClientIp = log.ClientIp,
                            Destination = log.Destination,
                            Status = log.IsSuccess ? "Success" : "Error"
                        });
                    }
                }
            }
            
            // Get Config
            var confReq = new IpcMessage { Type = IpcMessageTypes.GetConfiguration };
            var confResp = await _ipcClient.SendAsync(confReq);
            if (confResp.Type == IpcMessageTypes.ConfigurationResponse && confResp.Payload != null)
            {
                var conf = JsonSerializer.Deserialize<ProxyConfiguration>(confResp.Payload);
                if (conf != null)
                {
                    AllowedClientsCount = conf.AllowedClients.Count;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating dashboard: {ex.Message}");
        }
    }
}
