using System.Net;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Rate limiter for incoming discovery requests to prevent CPU flood and denial-of-service.
/// </summary>
public interface IDiscoveryRateLimiter
{
    /// <summary>
    /// Checks whether an incoming request from the specified client IP is allowed under the rate limit policy.
    /// </summary>
    /// <param name="clientAddress">The IP address of the discovering client.</param>
    /// <returns><c>true</c> if allowed; otherwise <c>false</c>.</returns>
    bool ShouldAllow(IPAddress clientAddress);

    /// <summary>
    /// Clears internal rate limiting history (used for tests or maintenance).
    /// </summary>
    void Reset();
}
