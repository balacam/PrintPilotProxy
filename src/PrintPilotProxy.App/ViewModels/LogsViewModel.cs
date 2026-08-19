using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;
using WpfClipboard = System.Windows.Clipboard;

namespace PrintPilotProxy.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "PrintPilotProxy", "logs");

    private static readonly Regex SerilogHeaderRegex = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s*[+-]\d{2}:?\d{2}|Z)?)\s+\[(?<level>[A-Za-z]{3,5})\]\s*(?<message>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex ShortHeaderRegex = new(
        @"^\[(?<timestamp>\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(?<level>[A-Za-z]{3,5})\]\s*(?<message>.*)$",
        RegexOptions.Compiled);

    private readonly IpcClientService? _ipc;

    [ObservableProperty] private string _logContent = string.Empty;
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _currentLogFile = string.Empty;

    [ObservableProperty] private bool _showAccessLogs = true;
    [ObservableProperty] private bool _showSystemLogs = false;
    [ObservableProperty] private bool _hasSystemLogs = false;

    public ObservableCollection<ActivityLogEntry> AccessLogs { get; } = new();
    public ObservableCollection<SystemLogEntry> SystemLogs { get; } = new();

    public LogsViewModel() : this(null) { }

    public LogsViewModel(IpcClientService? ipc)
    {
        _ipc = ipc;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            if (_ipc != null)
            {
                var requests = await _ipc.GetRecentRequestsAsync();
                AccessLogs.Clear();
                // Newest request on top
                foreach (var r in requests.OrderByDescending(x => x.Timestamp))
                {
                    AccessLogs.Add(new ActivityLogEntry
                    {
                        Time = r.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                        ClientIp = r.ClientIp,
                        Method = r.Method,
                        Destination = r.Destination,
                        Status = r.IsSuccess ? "OK" : "Error",
                        StatusBrushKey = r.IsSuccess ? "SuccessBrush" : "ErrorBrush"
                    });
                }
            }

            if (!Directory.Exists(LogDirectory))
            {
                LogContent = LocalizationService.Instance["Logs.NoDirectory"];
                CurrentLogFile = string.Empty;
                SystemLogs.Clear();
                HasSystemLogs = false;
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
                SystemLogs.Clear();
                HasSystemLogs = false;
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

            LogContent = content;

            var parsedEntries = ParseLogEntries(content, maxEntries: 500);
            SystemLogs.Clear();
            foreach (var entry in parsedEntries)
            {
                SystemLogs.Add(entry);
            }
            HasSystemLogs = SystemLogs.Count > 0;

            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.Loaded", parsedEntries.Count, Path.GetFileName(latestLog));
        }
        catch (Exception ex)
        {
            HasSystemLogs = false;
            StatusMessage = LocalizationService.Instance.GetFormat("Logs.Msgs.LoadError", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static List<SystemLogEntry> ParseLogEntries(string content, int maxEntries = 500)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<SystemLogEntry>();

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var entries = new List<SystemLogEntry>();

        SystemLogEntry? currentEntry = null;
        var detailsBuilder = new StringBuilder();

        void FlushCurrent()
        {
            if (currentEntry != null)
            {
                if (detailsBuilder.Length > 0)
                {
                    currentEntry.Details = detailsBuilder.ToString().TrimEnd();
                    detailsBuilder.Clear();
                }
                entries.Add(currentEntry);
                currentEntry = null;
            }
        }

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine) && currentEntry == null)
                continue;

            var match = SerilogHeaderRegex.Match(rawLine);
            if (!match.Success)
                match = ShortHeaderRegex.Match(rawLine);

            if (match.Success)
            {
                FlushCurrent();
                var rawTs = match.Groups["timestamp"].Value.Trim();
                var lvl = match.Groups["level"].Value.ToUpperInvariant().Trim();
                var msg = match.Groups["message"].Value;

                var displayTs = FormatTimestamp(rawTs);
                var (brushKey, surfaceKey) = GetLevelBrushes(lvl);

                currentEntry = new SystemLogEntry
                {
                    Timestamp = displayTs,
                    Level = lvl,
                    Message = msg,
                    LevelBrushKey = brushKey,
                    LevelSurfaceBrushKey = surfaceKey,
                    RawText = rawLine
                };
            }
            else
            {
                if (currentEntry != null)
                {
                    if (detailsBuilder.Length > 0)
                        detailsBuilder.AppendLine();
                    detailsBuilder.Append(rawLine);
                }
                else if (!string.IsNullOrWhiteSpace(rawLine))
                {
                    entries.Add(new SystemLogEntry
                    {
                        Timestamp = string.Empty,
                        Level = "LOG",
                        Message = rawLine,
                        LevelBrushKey = "SecondaryTextBrush",
                        LevelSurfaceBrushKey = "DarkBackgroundBrush",
                        RawText = rawLine
                    });
                }
            }
        }

        FlushCurrent();

        // Newest log on top (en son log en üstte olmalı)
        entries.Reverse();

        if (entries.Count > maxEntries)
        {
            return entries.Take(maxEntries).ToList();
        }

        return entries;
    }

    private static string FormatTimestamp(string rawTs)
    {
        if (string.IsNullOrWhiteSpace(rawTs))
            return string.Empty;

        // Strip trailing timezone offset like "+03:00" or " +03:00" or "Z" to keep clean fixed width
        var offsetMatch = Regex.Match(rawTs, @"^(?<main>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?)(?:\s*[+-]\d{2}:?\d{2}|Z)?$");
        if (offsetMatch.Success)
        {
            return offsetMatch.Groups["main"].Value;
        }

        return rawTs;
    }

    private static (string brushKey, string surfaceKey) GetLevelBrushes(string level) => level switch
    {
        "INF" or "INFO" or "INFORMATION" => ("InfoBrush", "InfoSurfaceBrush"),
        "WRN" or "WARN" or "WARNING"     => ("WarningBrush", "WarningSurfaceBrush"),
        "ERR" or "ERROR"                 => ("ErrorBrush", "ErrorSurfaceBrush"),
        "FTL" or "FATAL"                 => ("ErrorBrush", "ErrorSurfaceBrush"),
        "DBG" or "DEBUG"                 => ("MutedTextBrush", "DarkBackgroundBrush"),
        "VRB" or "VERBOSE" or "TRACE"    => ("MutedTextBrush", "DarkBackgroundBrush"),
        _                                => ("SecondaryTextBrush", "DarkBackgroundBrush")
    };

    [RelayCommand]
    private void ClearView()
    {
        // Clear the UI only — does NOT delete any log files
        SystemLogs.Clear();
        AccessLogs.Clear();
        LogContent = string.Empty;
        HasSystemLogs = false;
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
            if (ShowSystemLogs && SystemLogs.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var entry in SystemLogs)
                {
                    sb.AppendLine(entry.RawText);
                    if (entry.HasDetails)
                    {
                        sb.AppendLine(entry.Details);
                    }
                }
                WpfClipboard.SetText(sb.ToString());
                StatusMessage = LocalizationService.Instance["Logs.Msgs.Copied"];
            }
            else if (ShowAccessLogs && AccessLogs.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var a in AccessLogs)
                {
                    sb.AppendLine($"{a.Time}\t{a.ClientIp}\t{a.Method}\t{a.Destination}\t{a.Status}");
                }
                WpfClipboard.SetText(sb.ToString());
                StatusMessage = LocalizationService.Instance["Logs.Msgs.Copied"];
            }
            else if (!string.IsNullOrEmpty(LogContent))
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

public class SystemLogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = "INF";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
    public string LevelBrushKey { get; set; } = "InfoBrush";
    public string LevelSurfaceBrushKey { get; set; } = "InfoSurfaceBrush";
    public string RawText { get; set; } = string.Empty;
}
