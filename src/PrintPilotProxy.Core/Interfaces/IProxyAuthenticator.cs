using System.Net;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Result of an authentication evaluation.
/// </summary>
public sealed class AuthenticationResult
{
    public bool IsSuccess { get; }
    public string? FailureReason { get; }
    public int ProtocolVersion { get; }

    private AuthenticationResult(bool isSuccess, string? failureReason, int protocolVersion = 1)
    {
        IsSuccess = isSuccess;
        FailureReason = failureReason;
        ProtocolVersion = protocolVersion;
    }

    public static AuthenticationResult Success(int protocolVersion = 1) => new(true, null, protocolVersion);
    public static AuthenticationResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Handles automatic HMAC/protocol-level authentication between PrintPilot and PrintPilotProxy.
/// Prevents unauthenticated LAN applications from using PrintPilotProxy as an open forward proxy.
/// </summary>
public interface IProxyAuthenticator
{
    /// <summary>
    /// Whether automatic authentication is currently enabled.
    /// </summary>
    bool IsAuthenticationRequired { get; }

    /// <summary>
    /// Validates the client's proxy authorization header or token.
    /// </summary>
    /// <param name="authorizationHeader">The raw header value (e.g. Proxy-Authorization or X-PrintPilot-Auth).</param>
    /// <param name="clientIp">Client remote IP address.</param>
    /// <returns>The authentication result.</returns>
    AuthenticationResult Authenticate(string? authorizationHeader, IPAddress clientIp);

    /// <summary>
    /// Helper method to generate a valid authorization header value for PrintPilot clients and integration tests.
    /// </summary>
    /// <param name="protocolVersion">The protocol version (default 1).</param>
    /// <param name="nonce">Optional custom nonce (or auto-generated if null).</param>
    /// <param name="timestamp">Optional timestamp (or UtcNow if null).</param>
    /// <returns>A formatted authorization header string.</returns>
    string GenerateAuthHeader(int protocolVersion = 1, string? nonce = null, DateTimeOffset? timestamp = null);
}
