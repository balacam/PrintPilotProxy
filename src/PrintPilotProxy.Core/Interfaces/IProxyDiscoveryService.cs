using System.Net;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Event arguments for discovery events.
/// </summary>
public sealed class DiscoveryRequestEventArgs : EventArgs
{
    public DiscoveryRequest Request { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public DiscoveryRequestEventArgs(DiscoveryRequest request, IPEndPoint remoteEndPoint)
    {
        Request = request;
        RemoteEndPoint = remoteEndPoint;
    }
}

public sealed class DiscoveryResponseEventArgs : EventArgs
{
    public DiscoveryResponse Response { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public DiscoveryResponseEventArgs(DiscoveryResponse response, IPEndPoint remoteEndPoint)
    {
        Response = response;
        RemoteEndPoint = remoteEndPoint;
    }
}

/// <summary>
/// Core discovery service coordinating discovery requests, dynamic network interface matching,
/// instance identity, and response generation over discovery transports.
/// </summary>
public interface IProxyDiscoveryService : IAsyncDisposable
{
    /// <summary>
    /// Starts the discovery service.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the discovery service.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the discovery service is currently listening and active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets current runtime statistics and discovery status.
    /// </summary>
    DiscoveryStatus GetStatus();

    /// <summary>
    /// Raised when a valid discovery request is received.
    /// </summary>
    event EventHandler<DiscoveryRequestEventArgs>? RequestReceived;

    /// <summary>
    /// Raised when a discovery response is sent to a client.
    /// </summary>
    event EventHandler<DiscoveryResponseEventArgs>? ResponseSent;
}
