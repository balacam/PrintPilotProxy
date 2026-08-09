using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PrintPilotProxy.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IIpcClient _ipcClient;
    private DispatcherTimer? _timer;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _proxyStatus = "UNKNOWN";

    [ObservableProperty]
    private string _listenAddress = "Unknown";

    public MainViewModel(IIpcClient ipcClient)
    {
        _ipcClient = ipcClient;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (s, e) => await UpdateStatusAsync();
        _timer.Start();

        // Navigate("Dashboard");
    }

    private async Task UpdateStatusAsync()
    {
        try
        {
            if (!_ipcClient.IsConnected)
            {
                await _ipcClient.ConnectAsync();
            }

            var request = new IpcMessage { Type = IpcMessageTypes.GetStatus };
            var response = await _ipcClient.SendAsync(request);

            if (response.Type == IpcMessageTypes.StatusResponse && response.Payload != null)
            {
                var status = JsonSerializer.Deserialize<ProxyStatus>(response.Payload);
                if (status != null)
                {
                    ProxyStatus = status.State.ToString().ToUpperInvariant();
                    ListenAddress = status.ListeningAddress ?? "Unknown";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching status: {ex.Message}");
            ProxyStatus = "ERROR";
        }
    }

    [RelayCommand]
    private void Navigate(string pageName)
    {
        try
        {
            var viewType = Type.GetType($"PrintPilotProxy.App.Views.{pageName}Page");
            if (viewType != null)
            {
                var view = Activator.CreateInstance(viewType) as UserControl;
                var viewModelType = Type.GetType($"PrintPilotProxy.App.ViewModels.{pageName}ViewModel");
                if (viewModelType != null && view != null)
                {
                    view.DataContext = App.Current.Services.GetService(viewModelType);
                }
                if (view != null)
                {
                    CurrentPage = view;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }
}
