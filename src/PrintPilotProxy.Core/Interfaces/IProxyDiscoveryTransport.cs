using System.Net;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Abstraction for a discovery networking transport (UDP Broadcast, mDNS in future, etc.).
/// </summary>
public interface IProxyDiscoveryTransport : IAsyncDisposable
{
    /// <summary>
    /// Friendly transport name (e.g. "UDP Broadcast").
    /// </summary>
    string TransportName { get; }

    /// <summary>
    /// Port number used by this transport.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Whether the transport listener is active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the discovery transport listener.
    /// </summary>
    /// <param name="messageHandler">Handler delegate returning a discovery response for a given request and remote endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(Func<DiscoveryRequest, IPEndPoint, Task<DiscoveryResponse?>> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the discovery transport listener.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
