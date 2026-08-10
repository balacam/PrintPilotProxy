using System;
using System.IO;
using System.Text;
using System.Windows.Markup;

namespace PrintPilotProxy.App.Services;

/// <summary>
/// Persistent diagnostic logger recording GUI navigation, View/ViewModel creation,
/// DI resolution, XAML parsing line numbers, and IPC initialization events to ProgramData log files.
/// </summary>
public static class DiagnosticLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PrintPilotProxy", "logs");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                _logFilePath = Path.Combine(dir, "app_navigation_debug.log");
            }
            return _logFilePath;
        }
    }

    public static void Log(string message)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fffZ}] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        Console.WriteLine(line);

        lock (_lock)
        {
            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    var fallback = Path.Combine(Path.GetTempPath(), "PrintPilotProxy_nav.log");
                    File.AppendAllText(fallback, line + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }
    }

    public static void LogException(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"EXCEPTION in [{context}]:");
        sb.AppendLine($"  Type: {ex.GetType().FullName}");
        sb.AppendLine($"  Message: {ex.Message}");

        var baseEx = ex.GetBaseException();
        if (baseEx != null && baseEx != ex)
        {
            sb.AppendLine($"  BaseExceptionType: {baseEx.GetType().FullName}");
            sb.AppendLine($"  BaseExceptionMessage: {baseEx.Message}");
        }

        if (ex is XamlParseException xamlEx)
        {
            sb.AppendLine($"  XamlLineNumber: {xamlEx.LineNumber}");
            sb.AppendLine($"  XamlLinePosition: {xamlEx.LinePosition}");
            sb.AppendLine($"  Key/UriContext: {xamlEx.BaseUri}");
        }

        if (ex.InnerException != null)
        {
            sb.AppendLine($"  InnerType: {ex.InnerException.GetType().FullName}");
            sb.AppendLine($"  InnerMessage: {ex.InnerException.Message}");
            if (ex.InnerException is XamlParseException innerXamlEx)
            {
                sb.AppendLine($"  InnerXamlLineNumber: {innerXamlEx.LineNumber}");
                sb.AppendLine($"  InnerXamlLinePosition: {innerXamlEx.LinePosition}");
            }
        }

        sb.AppendLine($"  StackTrace:\n{ex.StackTrace}");
        Log(sb.ToString());
    }
}
