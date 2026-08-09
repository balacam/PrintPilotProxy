using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Abstraction over the forward proxy engine.
/// Allows the underlying proxy implementation to be replaced without
/// changing the rest of the application.
/// </summary>
public interface IProxyEngine : IAsyncDisposable
{
    /// <summary>
    /// Starts the proxy engine with the specified configuration.
    /// </summary>
    Task StartAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the proxy engine gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current proxy status.
    /// </summary>
    ProxyStatus GetStatus();

    /// <summary>
    /// Gets the most recent proxy request log entries.
    /// </summary>
    IReadOnlyList<ProxyRequestEntry> GetRecentRequests(int count = 100);

    /// <summary>
    /// Raised when a proxy request is processed.
    /// </summary>
    event EventHandler<ProxyRequestEntry>? RequestProcessed;

    /// <summary>
    /// Raised when a proxy error occurs.
    /// </summary>
    event EventHandler<ProxyErrorEventArgs>? ErrorOccurred;
}

/// <summary>
/// Event arguments for proxy errors.
/// </summary>
public sealed class ProxyErrorEventArgs : EventArgs
{
    /// <summary>The exception that occurred.</summary>
    public Exception Exception { get; }

    /// <summary>Context description of where the error occurred.</summary>
    public string Context { get; }

    /// <summary>Client IP if available.</summary>
    public string? ClientIp { get; }

    public ProxyErrorEventArgs(Exception exception, string context, string? clientIp = null)
    {
        Exception = exception;
        Context = context;
        ClientIp = clientIp;
    }
}
