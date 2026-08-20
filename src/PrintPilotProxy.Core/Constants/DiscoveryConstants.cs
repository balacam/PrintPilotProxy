namespace PrintPilotProxy.Core.Constants;

/// <summary>
/// Well-known protocol constants for PrintPilotProxy discovery and automatic authentication.
/// </summary>
public static class DiscoveryConstants
{
    /// <summary>
    /// Service identifier in discovery messages.
    /// </summary>
    public const string ServiceName = "PrintPilotProxy";

    /// <summary>
    /// Supported discovery and authentication protocol version.
    /// </summary>
    public const int ProtocolVersion = 1;

    /// <summary>
    /// Default UDP port for PrintPilotProxy discovery on LAN.
    /// </summary>
    public const int DefaultUdpPort = 37421;

    /// <summary>
    /// Forward proxy protocol identifier.
    /// </summary>
    public const string HttpConnectProtocol = "http-connect";

    /// <summary>
    /// Authentication scheme identifier.
    /// </summary>
    public const string AuthScheme = "PrintPilot-HMAC";

    /// <summary>
    /// Default action in discovery request payload.
    /// </summary>
    public const string DiscoverRequestAction = "discover";
}
