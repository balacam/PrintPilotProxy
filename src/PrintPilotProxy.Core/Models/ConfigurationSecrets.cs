namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Placeholder that stands in for a stored secret in configuration sent to IPC clients.
/// A client that sends it back unchanged keeps the stored secret; the service never returns the real ciphertext.
/// </summary>
public static class ConfigurationSecrets
{
    public const string UnchangedSentinel = "(unchanged)";

    /// <summary>The value shown to clients for a stored secret: the placeholder, or nothing when no secret is set.</summary>
    public static string? Mask(string? stored) => string.IsNullOrEmpty(stored) ? stored : UnchangedSentinel;

    /// <summary>The value to store: the stored secret when the client sent the placeholder back, otherwise what it sent.</summary>
    public static string? Resolve(string? submitted, string? stored) => submitted == UnchangedSentinel ? stored : submitted;
}
