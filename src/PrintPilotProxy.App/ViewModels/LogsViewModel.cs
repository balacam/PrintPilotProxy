using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using WpfClipboard = System.Windows.Clipboard;

namespace PrintPilotProxy.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "PrintPilotProxy", "logs");

    [ObservableProperty] private string _logContent = string.Empty;
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _currentLogFile = string.Empty;

    public LogsViewModel()
    {
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            if (!Directory.Exists(LogDirectory))
            {
                LogContent = LocalizationService.Instance["Logs.NoDirectory"];
                CurrentLogFile = string.Empty;
                return;
            }

            // Find today's log file
            var today = DateTime.Now.ToString("yyyyMMdd");
            var logFiles = Directory.GetFiles(LogDirectory, $"*{today}*.log");

            // If no today file, get the most recent one
            if (logFiles.Length == 0)
                logFiles = Directory.GetFiles(LogDirectory, "*.log");

            if (logFiles.Length == 0)
            {
                LogContent = LocalizationService.Instance["Logs.NoFiles"];
                CurrentLogFile = string.Empty;
                return;
            }

            // Sort by last write time descending
            Array.Sort(logFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            var latestLog = logFiles[0];
            CurrentLogFile = latestLog;

            // Read with shared read access (log file may be actively written)
            using var fs = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var content = await reader.ReadToEndAsync();

            // Show last 500 lines to avoid UI overload
            var lines = content.Split('\n');
            if (lines.Length > 500)
                LogContent = string.Join('\n', lines[^500..]);
            else
                LogContent = content;

            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.Loaded", lines.Length, Path.GetFileName(latestLog));
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.LoadError", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearView()
    {
        // Clear the UI only — does NOT delete any log files
        LogContent = string.Empty;
        StatusMessage = LocalizationService.Instance["Logs.Msgs.ViewCleared"];
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            Process.Start("explorer.exe", LogDirectory);
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.OpenFolderFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        try
        {
            if (!string.IsNullOrEmpty(LogContent))
            {
                WpfClipboard.SetText(LogContent);
                StatusMessage = LocalizationService.Instance["Logs.Msgs.Copied"];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.CopyFailed", ex.Message);
        }
    }
}
