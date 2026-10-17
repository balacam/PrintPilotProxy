using System;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Validation;

/// <summary>Turns the configured logging limits into values the file log sink can use.</summary>
public static class LoggingPolicy
{
    public const int DefaultRetentionDays = 30;
    public const int DefaultMaxSizeMb = 100;

    /// <summary>Daily files kept; at least one, at most a year.</summary>
    public static int RetainedFileCount(LoggingSettings? settings)
    {
        var days = settings?.RetentionDays ?? DefaultRetentionDays;
        return Math.Clamp(days <= 0 ? DefaultRetentionDays : days, 1, 365);
    }

    /// <summary>
    /// Size limit per log file. The configured total is spread over the retained files so the folder stays near it.
    /// Never below 1 MB.
    /// </summary>
    public static long FileSizeLimitBytes(LoggingSettings? settings)
    {
        var totalMb = settings?.MaxSizeMb ?? DefaultMaxSizeMb;
        if (totalMb <= 0) totalMb = DefaultMaxSizeMb;
        var perFileMb = Math.Max(1, totalMb / RetainedFileCount(settings));
        return perFileMb * 1024L * 1024L;
    }

    /// <summary>Parses the configured minimum level; unknown or empty values fall back to Information.</summary>
    public static string NormalizeMinimumLevel(LoggingSettings? settings)
    {
        var value = settings?.MinimumLevel;
        return value is not null && Enum.TryParse<LogLevelName>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : nameof(LogLevelName.Information);
    }

    public enum LogLevelName
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }
}
