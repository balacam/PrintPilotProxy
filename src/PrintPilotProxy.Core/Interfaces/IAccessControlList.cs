using System.Net;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Evaluates whether a client IP is authorized to use the proxy
/// based on the configured ACL rules.
/// </summary>
public interface IAccessControlList
{
    /// <summary>
    /// Returns whether the specified client IP is allowed to use the proxy.
    /// </summary>
    bool IsAllowed(IPAddress clientAddress);

    /// <summary>
    /// Returns whether the specified destination port is allowed.
    /// </summary>
    bool IsDestinationPortAllowed(int port);

    /// <summary>
    /// Refreshes the ACL from the current configuration.
    /// </summary>
    void Refresh(Models.ProxyConfiguration configuration);
}
