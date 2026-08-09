using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Performs security audits on the current proxy configuration.
/// </summary>
public interface ISecurityAuditor
{
    /// <summary>
    /// Runs all security checks and returns the audit result.
    /// </summary>
    SecurityAudit Audit(ProxyConfiguration configuration);
}

/// <summary>
/// Runs diagnostic tests.
/// </summary>
public interface IDiagnosticsRunner
{
    /// <summary>
    /// Runs all diagnostic tests and returns results.
    /// </summary>
    Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a specific diagnostic test by ID.
    /// </summary>
    Task<DiagnosticResult> RunTestAsync(string testId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Named pipe IPC client for communicating with the service.
/// </summary>
public interface IIpcClient : IAsyncDisposable
{
    /// <summary>Sends a message and waits for a response.</summary>
    Task<IpcMessage> SendAsync(IpcMessage message, CancellationToken cancellationToken = default);

    /// <summary>Whether the client is connected to the service.</summary>
    bool IsConnected { get; }

    /// <summary>Attempts to connect to the service.</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Named pipe IPC server hosted by the service.
/// </summary>
public interface IIpcServer : IAsyncDisposable
{
    /// <summary>Starts listening for client connections.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when a message is received from a client.</summary>
    event Func<IpcMessage, Task<IpcMessage>>? MessageReceived;
}
