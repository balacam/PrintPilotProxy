using System.Text.Json.Serialization;
using PrintPilotProxy.Core.Constants;

namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Discovery request message sent by PrintPilot clients on LAN over UDP.
/// </summary>
public sealed class DiscoveryRequest
{
    /// <summary>
    /// The target service name (e.g., "PrintPilotProxy").
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = DiscoveryConstants.ServiceName;

    /// <summary>
    /// Protocol version supported by client (default 1).
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = DiscoveryConstants.ProtocolVersion;

    /// <summary>
    /// The request action (e.g., "discover").
    /// </summary>
    [JsonPropertyName("request")]
    public string Request { get; set; } = DiscoveryConstants.DiscoverRequestAction;

    /// <summary>
    /// Optional extension data for future protocol extensions.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// Discovery response message returned by PrintPilotProxy to discovering clients.
/// </summary>
public sealed class DiscoveryResponse
{
    /// <summary>
    /// The service name identifier ("PrintPilotProxy").
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = DiscoveryConstants.ServiceName;

    /// <summary>
    /// Protocol version of the response (default 1).
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = DiscoveryConstants.ProtocolVersion;

    /// <summary>
    /// Application version (e.g. "0.5.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.5.0";

    /// <summary>
    /// The host IP address on which PrintPilotProxy is reachable for the client.
    /// </summary>
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The port on which the proxy engine is listening (e.g. 3128, 8080).
    /// </summary>
    [JsonPropertyName("proxyPort")]
    public int ProxyPort { get; set; }

    /// <summary>
    /// Unique and persistent instance ID for this installation.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// The supported proxy protocol ("http-connect").
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = DiscoveryConstants.HttpConnectProtocol;

    /// <summary>
    /// Authentication protocol supported ("PrintPilot-HMAC").
    /// </summary>
    [JsonPropertyName("authProtocol")]
    public string AuthProtocol { get; set; } = DiscoveryConstants.AuthScheme;

    /// <summary>
    /// Friendly interface name handling this request (optional, for diagnostics).
    /// </summary>
    [JsonPropertyName("interfaceName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterfaceName { get; set; }

    /// <summary>
    /// Optional status of the proxy engine ("Running", "Starting", etc.).
    /// </summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    /// <summary>
    /// Optional extension data.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// Runtime status information for the Discovery Service.
/// </summary>
public sealed class DiscoveryStatus
{
    /// <summary>
    /// Whether the discovery listener is currently running.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Port being listened on (UDP 37421).
    /// </summary>
    public int Port { get; set; } = DiscoveryConstants.DefaultUdpPort;

    /// <summary>
    /// Total discovery requests received since startup.
    /// </summary>
    public long TotalRequestsReceived { get; set; }

    /// <summary>
    /// Total discovery responses successfully sent.
    /// </summary>
    public long TotalResponsesSent { get; set; }

    /// <summary>
    /// Total requests dropped/throttled by the rate limiter.
    /// </summary>
    public long ThrottledRequests { get; set; }

    /// <summary>
    /// Timestamp of last received discovery request.
    /// </summary>
    public DateTimeOffset? LastRequestReceivedAt { get; set; }

    /// <summary>
    /// Last error message if any occurred.
    /// </summary>
    public string? LastError { get; set; }
}
