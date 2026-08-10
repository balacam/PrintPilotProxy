using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.App.Views;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly Dictionary<string, (Type ViewType, Type ViewModelType)> PageRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"]       = (typeof(DashboardPage),       typeof(DashboardViewModel)),
        ["NetworkSettings"] = (typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel)),
        ["ProxySettings"]   = (typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel)),
        ["Settings"]        = (typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel)),
        ["AllowedClients"]  = (typeof(AllowedClientsPage),  typeof(AllowedClientsViewModel)),
        ["Firewall"]        = (typeof(FirewallPage),        typeof(FirewallViewModel)),
        ["Service"]         = (typeof(ServicePage),         typeof(ServiceViewModel)),
        ["Logs"]            = (typeof(LogsPage),            typeof(LogsViewModel)),
        ["Diagnostics"]     = (typeof(DiagnosticsPage),     typeof(DiagnosticsViewModel)),
        ["Security"]        = (typeof(SecurityPage),        typeof(SecurityViewModel)),
        ["Language"]        = (typeof(LanguagePage),        typeof(LanguageViewModel)),
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IpcClientService _ipc;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _proxyStatus = "Unknown";
    [ObservableProperty] private string _listenAddress = "N/A";
    [ObservableProperty] private bool _isConnectedToService = false;

    public MainViewModel(IServiceProvider serviceProvider, IpcClientService ipc)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ipc = ipc ?? throw new ArgumentNullException(nameof(ipc));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await UpdateStatusAsync();
        _timer.Start();

        // Navigate to Dashboard by default
        Navigate("Dashboard");
    }

    private async Task UpdateStatusAsync()
    {
        try
        {
            var status = await _ipc.GetStatusAsync();
            if (status != null)
            {
                IsConnectedToService = true;
                ProxyStatus = status.State.ToString().ToUpperInvariant();
                ListenAddress = status.ListeningAddress ?? "N/A";
            }
            else
            {
                IsConnectedToService = false;
                ProxyStatus = LocalizationService.Instance["MainWindow.ServiceUnavailable"];
                ListenAddress = LocalizationService.Instance["Common.Na"];
            }
        }
        catch
        {
            IsConnectedToService = false;
            ProxyStatus = LocalizationService.Instance["MainWindow.ServiceUnavailable"];
            ListenAddress = LocalizationService.Instance["Common.Na"];
        }
    }

    [RelayCommand]
    private void Navigate(string pageName)
    {
        DiagnosticLogger.Log($"[1. Settings Button Clicked / 2. Navigation Command Executed] Requested route: '{pageName}'");

        try
        {
            if (string.IsNullOrWhiteSpace(pageName))
            {
                DiagnosticLogger.Log("[3. Route Invalid] Page name parameter was null or empty.");
                return;
            }

            var requestedName = pageName.Trim();
            DiagnosticLogger.Log($"[3. Requested route]: '{requestedName}'");

            if (!PageRoutes.TryGetValue(requestedName, out var route))
            {
                DiagnosticLogger.Log($"[4. Route Unmapped] Could not resolve page route for '{requestedName}'.");
                MessageBox.Show($"Navigation target '{requestedName}' is not recognized.", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var viewType = route.ViewType;
            var viewModelType = route.ViewModelType;
            DiagnosticLogger.Log($"[4. Normalized route]: '{viewType.Name}' | [5. Resolved View type]: '{viewType.FullName}' | [6. Resolved ViewModel type]: '{viewModelType.FullName}'");

            UserControl view;
            try
            {
                var instantiated = Activator.CreateInstance(viewType);
                if (instantiated is not UserControl uc)
                {
                    DiagnosticLogger.Log($"[8. View Creation Failed] Created object was not UserControl: '{instantiated?.GetType().FullName}'");
                    return;
                }
                view = uc;
                DiagnosticLogger.Log($"[8. View Creation Succeeded] View '{viewType.Name}' instantiated.");
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogException($"8. View Creation Failed for '{viewType.Name}'", ex);
                MessageBox.Show($"Failed to instantiate View '{viewType.Name}':\n{ex.Message}", "View Instantiation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            object? vm = null;
            try
            {
                vm = _serviceProvider.GetService(viewModelType);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogException($"7. DI Resolution Exception for '{viewModelType.Name}'", ex);
            }

            if (vm != null)
            {
                DiagnosticLogger.Log($"[7. ViewModel resolved through DI]: '{viewModelType.Name}'");
                view.DataContext = vm;
            }
            else
            {
                DiagnosticLogger.Log($"[7. DI Resolution Failed] Could not resolve ViewModel '{viewModelType.Name}' from IServiceProvider.");
                MessageBox.Show($"Failed to resolve ViewModel '{viewModelType.Name}' from DI service provider.", "DI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DiagnosticLogger.Log($"[9. IPC Connection Attempted] IsConnected={_ipc.IsConnected}");
            CurrentPage = view;
            DiagnosticLogger.Log($"[10. Navigation Complete] CurrentPage set to '{viewType.Name}'");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException($"11-14. Global Navigation Exception for '{pageName}'", ex);
            MessageBox.Show($"Navigation Error to '{pageName}':\n{ex.Message}\n\nLog file: {DiagnosticLogger.LogFilePath}", "Navigation Failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
