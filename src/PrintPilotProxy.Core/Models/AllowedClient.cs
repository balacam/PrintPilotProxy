using System.Net;
using System.Text.Json.Serialization;

namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Represents a client or network that is allowed to use the proxy.
/// </summary>
public sealed class AllowedClient
{
    /// <summary>
    /// Unique identifier for this client entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Human-friendly name for the client (e.g., "PrintPilot-PC-01").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IP address or CIDR notation (e.g., "192.168.10.50" or "192.168.10.0/24").
    /// </summary>
    public string IpOrCidr { get; set; } = string.Empty;

    /// <summary>
    /// Optional description (e.g., "Accounting workstation").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this client entry is currently enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Date/time when this entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Date/time when this entry was last modified.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns whether this entry represents a CIDR range rather than a single IP.
    /// </summary>
    [JsonIgnore]
    public bool IsCidr => IpOrCidr.Contains('/');

    /// <summary>
    /// Returns the CIDR prefix length, or null if this is a single IP.
    /// </summary>
    [JsonIgnore]
    public int? PrefixLength
    {
        get
        {
            if (!IsCidr) return null;
            var parts = IpOrCidr.Split('/');
            return parts.Length == 2 && int.TryParse(parts[1], out var prefix) ? prefix : null;
        }
    }
}
