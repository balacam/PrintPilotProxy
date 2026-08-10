using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.App.Views;
using WF = System.Windows.Forms;

namespace PrintPilotProxy.App;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    private MainWindow? _mainWindow;
    private WF.NotifyIcon? _notifyIcon;

    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply saved language preference before showing UI
        try
        {
            var ipc = Services.GetRequiredService<IpcClientService>();
            var config = await ipc.GetConfigurationAsync();
            if (config?.Language?.CultureName != null)
                LocalizationService.Instance.SetCulture(config.Language.CultureName);
        }
        catch { /* Service may not be available yet — use default culture */ }

        InitTrayIcon();
        ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    // ─── System tray ─────────────────────────────────────────────────────────

    private void InitTrayIcon()
    {
        var contextMenu = new WF.ContextMenuStrip();

        var miOpen = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.Open"])
        {
            Font = new System.Drawing.Font(contextMenu.Font, System.Drawing.FontStyle.Bold)
        };
        miOpen.Click += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        contextMenu.Items.Add(miOpen);
        contextMenu.Items.Add(new WF.ToolStripSeparator());

        var miStart = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.StartProxy"]);
        miStart.Click += async (_, _) =>
        {
            var ipc = Services.GetRequiredService<IpcClientService>();
            var (ok, msg) = await ipc.StartProxyAsync();
            if (!ok) Dispatcher.Invoke(() =>
                MessageBox.Show(msg, "PrintPilotProxy", MessageBoxButton.OK, MessageBoxImage.Warning));
        };
        contextMenu.Items.Add(miStart);

        var miStop = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.StopProxy"]);
        miStop.Click += async (_, _) =>
        {
            var ipc = Services.GetRequiredService<IpcClientService>();
            var (ok, msg) = await ipc.StopProxyAsync();
            if (!ok) Dispatcher.Invoke(() =>
                MessageBox.Show(msg, "PrintPilotProxy", MessageBoxButton.OK, MessageBoxImage.Warning));
        };
        contextMenu.Items.Add(miStop);

        var miRestart = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.RestartProxy"]);
        miRestart.Click += async (_, _) =>
        {
            var ipc = Services.GetRequiredService<IpcClientService>();
            var (ok, msg) = await ipc.RestartProxyAsync();
            if (!ok) Dispatcher.Invoke(() =>
                MessageBox.Show(msg, "PrintPilotProxy", MessageBoxButton.OK, MessageBoxImage.Warning));
        };
        contextMenu.Items.Add(miRestart);
        contextMenu.Items.Add(new WF.ToolStripSeparator());

        var miDiag = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.Diagnostics"]);
        miDiag.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            ShowMainWindow();
            (_mainWindow?.DataContext as MainViewModel)?.NavigateCommand.Execute("Diagnostics");
        });
        contextMenu.Items.Add(miDiag);

        var miLogs = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.OpenLogFolder"]);
        miLogs.Click += (_, _) => OpenLogFolder();
        contextMenu.Items.Add(miLogs);
        contextMenu.Items.Add(new WF.ToolStripSeparator());

        var miExit = new WF.ToolStripMenuItem(LocalizationService.Instance["Tray.Exit"]);
        miExit.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            // Closes the WPF GUI only — Windows Service keeps running
            _notifyIcon?.Dispose();
            Shutdown();
        });
        contextMenu.Items.Add(miExit);

        _notifyIcon = new WF.NotifyIcon
        {
            Text    = "PrintPilotProxy",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        try
        {
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(icoPath))
                _notifyIcon.Icon = new System.Drawing.Icon(icoPath);
        }
        catch { /* Icon not critical — tray still works */ }

        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }

    // ─── Window ───────────────────────────────────────────────────────────────

    internal void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = Services.GetRequiredService<MainWindow>();
            _mainWindow.Closing += (_, args) =>
            {
                args.Cancel = true;   // hide to tray instead of closing
                _mainWindow.Hide();
            };
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private static void OpenLogFolder()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PrintPilotProxy", "logs");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);
        Process.Start("explorer.exe", logDir);
    }

    // ─── DI ──────────────────────────────────────────────────────────────────

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        PrintPilotProxy.Infrastructure.InfrastructureServiceExtensions.AddInfrastructureServices(services);
        services.AddSingleton<IpcClientService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<NetworkSettingsViewModel>();
        services.AddTransient<ProxySettingsViewModel>();
        services.AddTransient<AllowedClientsViewModel>();
        services.AddTransient<FirewallViewModel>();
        services.AddTransient<ServiceViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<LanguageViewModel>();
        services.AddTransient<SecurityViewModel>();
        return services.BuildServiceProvider();
    }
}
