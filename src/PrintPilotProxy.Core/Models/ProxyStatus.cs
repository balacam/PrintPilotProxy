namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Current runtime status of the proxy engine.
/// </summary>
public sealed class ProxyStatus
{
    /// <summary>
    /// Current state of the proxy engine.
    /// </summary>
    public ProxyState State { get; set; } = ProxyState.Stopped;

    /// <summary>
    /// The address the proxy is listening on (e.g., "192.168.10.10:3128").
    /// </summary>
    public string? ListeningAddress { get; set; }

    /// <summary>
    /// When the proxy was last started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Total number of requests processed since last start.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Total number of errors since last start.
    /// </summary>
    public long TotalErrors { get; set; }

    /// <summary>
    /// Total bytes transferred since last start.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Timestamp of the last successful request.
    /// </summary>
    public DateTimeOffset? LastSuccessfulRequest { get; set; }

    /// <summary>
    /// Timestamp of the last failed request.
    /// </summary>
    public DateTimeOffset? LastFailedRequest { get; set; }

    /// <summary>
    /// Number of currently active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Current uptime, or null if not running.
    /// </summary>
    public TimeSpan? Uptime => State == ProxyState.Running && StartedAt.HasValue
        ? DateTimeOffset.UtcNow - StartedAt.Value
        : null;

    /// <summary>
    /// Name of the proxy engine.
    /// </summary>
    public string EngineName { get; set; } = "Unobtanium Web Proxy";

    /// <summary>
    /// Version of the proxy engine.
    /// </summary>
    public string EngineVersion { get; set; } = string.Empty;
}

/// <summary>
/// Possible states of the proxy engine.
/// </summary>
public enum ProxyState
{
    /// <summary>Proxy is stopped.</summary>
    Stopped,

    /// <summary>Proxy is starting up.</summary>
    Starting,

    /// <summary>Proxy is running and accepting connections.</summary>
    Running,

    /// <summary>Proxy is stopping.</summary>
    Stopping,

    /// <summary>Proxy has encountered a fatal error.</summary>
    Faulted
}

/// <summary>
/// Represents a single proxy request log entry.
/// </summary>
public sealed class ProxyRequestEntry
{
    /// <summary>Timestamp of the request.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Client IP address.</summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>HTTP method (GET, POST, CONNECT, etc.).</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Destination host and port.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>Bytes transferred.</summary>
    public long BytesTransferred { get; set; }

    /// <summary>Request duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Whether the request was successful.</summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 400;

    /// <summary>Error message if the request failed.</summary>
    public string? ErrorMessage { get; set; }
}
