namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Result of a security audit check.
/// </summary>
public sealed class SecurityAudit
{
    /// <summary>
    /// Individual security check results.
    /// </summary>
    public List<SecurityCheck> Checks { get; set; } = new();

    /// <summary>
    /// Overall security status.
    /// </summary>
    public SecurityLevel OverallLevel =>
        Checks.Any(c => c.Level == SecurityLevel.Critical) ? SecurityLevel.Critical :
        Checks.Any(c => c.Level == SecurityLevel.Warning) ? SecurityLevel.Warning :
        SecurityLevel.Secure;

    /// <summary>
    /// Timestamp of the audit.
    /// </summary>
    public DateTimeOffset AuditedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A single security check result.
/// </summary>
public sealed class SecurityCheck
{
    /// <summary>Check identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name of the check.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what this check verifies.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the check passed.</summary>
    public bool Passed { get; set; }

    /// <summary>Security level of this finding.</summary>
    public SecurityLevel Level { get; set; } = SecurityLevel.Secure;

    /// <summary>Detailed message about the finding.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Arguments to format the localized message.</summary>
    public string[] MessageArgs { get; set; } = Array.Empty<string>();

    /// <summary>Remediation advice if the check failed.</summary>
    public string? Remediation { get; set; }
}

/// <summary>
/// Security severity levels.
/// </summary>
public enum SecurityLevel
{
    /// <summary>No issues found.</summary>
    Secure,

    /// <summary>Informational notice.</summary>
    Info,

    /// <summary>Potential security concern that should be reviewed.</summary>
    Warning,

    /// <summary>Critical security issue that must be addressed.</summary>
    Critical
}
