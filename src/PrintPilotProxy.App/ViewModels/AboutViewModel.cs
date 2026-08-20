using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using WpfClipboard = System.Windows.Clipboard;

namespace PrintPilotProxy.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _appName = "PrintPilotProxy";

    [ObservableProperty]
    private string _version = "0.5.0";

    [ObservableProperty]
    private string _informationalVersion = "0.5.0";

    [ObservableProperty]
    private string _frameworkDescription = ".NET 8.0";

    [ObservableProperty]
    private string _osDescription = string.Empty;

    [ObservableProperty]
    private string _architecture = string.Empty;

    [ObservableProperty]
    private string _installPath = string.Empty;

    [ObservableProperty]
    private string _processId = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

    public AboutViewModel()
    {
        InitializeSystemInformation();
    }

    private void InitializeSystemInformation()
    {
        var assembly = typeof(AboutViewModel).Assembly;
        var version = assembly.GetName().Version;
        Version = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.5.0";

        var infoVerAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        InformationalVersion = infoVerAttr?.InformationalVersion ?? Version;

        FrameworkDescription = RuntimeInformation.FrameworkDescription;
        OsDescription = RuntimeInformation.OSDescription;
        Architecture = $"{RuntimeInformation.ProcessArchitecture} ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})";
        InstallPath = AppContext.BaseDirectory;
        ProcessId = Environment.ProcessId.ToString();
    }

    [RelayCommand]
    private async Task CopySystemInfoAsync()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Application: {AppName}");
            sb.AppendLine($"Version: {Version} ({InformationalVersion})");
            sb.AppendLine($"Runtime: {FrameworkDescription}");
            sb.AppendLine($"Operating System: {OsDescription}");
            sb.AppendLine($"Architecture: {Architecture}");
            sb.AppendLine($"Install Directory: {InstallPath}");
            sb.AppendLine($"Process ID: {ProcessId}");
            sb.AppendLine($"CLR Version: {Environment.Version}");

            WpfClipboard.SetText(sb.ToString());

            StatusIsError = false;
            StatusMessage = LocalizationService.Instance["About.SysInfoCopied"];

            await Task.Delay(3000);
            if (StatusMessage == LocalizationService.Instance["About.SysInfoCopied"])
            {
                StatusMessage = null;
            }
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenLogsDirectory()
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PrintPilotProxy", "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenConfigDirectory()
    {
        try
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PrintPilotProxy");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = configDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = ex.Message;
        }
    }
}
