namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Messages exchanged between the WPF application and the Windows Service via named pipes.
/// </summary>
public sealed class IpcMessage
{
    /// <summary>Message type identifier.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON payload.</summary>
    public string? Payload { get; set; }

    /// <summary>Correlation ID for request/response matching.</summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Timestamp of the message.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Caller security identity attached by the IPC server.</summary>
    public IpcClientIdentity? ClientIdentity { get; set; }
}

/// <summary>
/// Well-known IPC message types.
/// </summary>
public static class IpcMessageTypes
{
    // Requests
    public const string GetStatus = "GetStatus";
    public const string StartProxy = "StartProxy";
    public const string StopProxy = "StopProxy";
    public const string RestartProxy = "RestartProxy";
    public const string GetConfiguration = "GetConfiguration";
    public const string UpdateConfiguration = "UpdateConfiguration";
    public const string GetRecentRequests = "GetRecentRequests";
    public const string RunDiagnostics = "RunDiagnostics";
    public const string GetSecurityAudit = "GetSecurityAudit";
    public const string GetNetworkInterfaces = "GetNetworkInterfaces";
    public const string GetFirewallStatus = "GetFirewallStatus";
    public const string ApplyFirewallRule = "ApplyFirewallRule";
    public const string RemoveFirewallRule = "RemoveFirewallRule";

    // Responses
    public const string StatusResponse = "StatusResponse";
    public const string ConfigurationResponse = "ConfigurationResponse";
    public const string RecentRequestsResponse = "RecentRequestsResponse";
    public const string DiagnosticsResponse = "DiagnosticsResponse";
    public const string SecurityAuditResponse = "SecurityAuditResponse";
    public const string NetworkInterfacesResponse = "NetworkInterfacesResponse";
    public const string FirewallStatusResponse = "FirewallStatusResponse";

    // Generic
    public const string Error = "Error";
    public const string Success = "Success";
}
