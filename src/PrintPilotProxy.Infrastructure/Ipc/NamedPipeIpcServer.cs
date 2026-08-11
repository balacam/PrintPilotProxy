using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>
/// Local-only management endpoint for the Windows Service. The pipe ACL grants
/// access to authenticated users, while message dispatch validates client identity
/// and privilege levels for mutating commands.
/// </summary>
public sealed class NamedPipeIpcServer : IIpcServer, IAsyncDisposable
{
    public const string PipeName = "PrintPilotProxy";
    private const int MaxMessageCharacters = 1024 * 1024;

    private readonly ILogger<NamedPipeIpcServer> _logger;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public event Func<IpcMessage, Task<IpcMessage>>? MessageReceived;

    public NamedPipeIpcServer(ILogger<NamedPipeIpcServer>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NamedPipeIpcServer>.Instance;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation("IPC server started on local pipe {PipeName}.", PipeName);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            if (_serverTask is not null)
            {
                await _serverTask.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal service shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping the IPC server.");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _serverTask = null;
            _logger.LogInformation("IPC server stopped.");
        }
    }

    private async Task ServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stream = CreateSecureServerStream();
                await stream.WaitForConnectionAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using (stream)
                        {
                            await HandleConnectionAsync(stream, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling IPC client connection.");
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IPC server loop.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        while (stream.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length > MaxMessageCharacters)
            {
                await WriteResponseAsync(writer, new IpcMessage
                {
                    Type = IpcMessageTypes.Error,
                    Payload = "IPC message exceeds the permitted size."
                }, cancellationToken);
                break;
            }

            IpcMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<IpcMessage>(line);
            }
            catch (JsonException)
            {
                await WriteResponseAsync(writer, new IpcMessage
                {
                    Type = IpcMessageTypes.Error,
                    Payload = "Invalid IPC message."
                }, cancellationToken);
                continue;
            }

            if (message is null || string.IsNullOrWhiteSpace(message.Type))
            {
                await WriteResponseAsync(writer, new IpcMessage
                {
                    Type = IpcMessageTypes.Error,
                    CorrelationId = message?.CorrelationId ?? Guid.NewGuid().ToString("N"),
                    Payload = "IPC message type is required."
                }, cancellationToken);
                continue;
            }

            // Resolve and attach caller identity context
            message.ClientIdentity = ResolveCallerIdentity(stream);

            IpcMessage response;
            try
            {
                response = MessageReceived is null
                    ? new IpcMessage
                    {
                        Type = IpcMessageTypes.Error,
                        CorrelationId = message.CorrelationId,
                        Payload = "Service management endpoint is not ready."
                    }
                    : await MessageReceived(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing IPC message {MessageType}.", message.Type);
                response = new IpcMessage
                {
                    Type = IpcMessageTypes.Error,
                    CorrelationId = message.CorrelationId,
                    Payload = "The service could not process the management request."
                };
            }

            await WriteResponseAsync(writer, response, cancellationToken);
        }
    }

    private IpcClientIdentity ResolveCallerIdentity(NamedPipeServerStream stream)
    {
        if (!OperatingSystem.IsWindows())
        {
            return IpcClientIdentity.CreateInteractiveUser();
        }

        return ResolveWindowsCallerIdentity(stream);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IpcClientIdentity ResolveWindowsCallerIdentity(NamedPipeServerStream stream)
    {
        try
        {
            IpcClientIdentity? identity = null;
            stream.RunAsClient(() =>
            {
                using var windowsIdentity = WindowsIdentity.GetCurrent();
                var userSid = windowsIdentity.User?.Value ?? string.Empty;
                var name = windowsIdentity.Name;
                var isSystem = windowsIdentity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;
                var isAdmin = new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
                var isInteractive = windowsIdentity.User?.IsWellKnown(WellKnownSidType.InteractiveSid) == true
                                 || windowsIdentity.Groups?.Any(g => (g as SecurityIdentifier)?.IsWellKnown(WellKnownSidType.InteractiveSid) == true) == true;

                int activeConsoleSessionId = 0;
                try { activeConsoleSessionId = (int)GetActiveConsoleSessionId(); } catch { }
                int callerSessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;

                bool isActiveConsoleUser = (callerSessionId == activeConsoleSessionId && callerSessionId != 0) || isSystem || isAdmin || isInteractive;

                identity = new IpcClientIdentity
                {
                    UserSid = userSid,
                    Name = string.IsNullOrWhiteSpace(name) ? "DesktopUser" : name,
                    SessionId = callerSessionId,
                    IsLocalSystem = isSystem,
                    IsAdministrator = isAdmin,
                    IsInteractiveUser = isInteractive,
                    IsActiveConsoleUser = isActiveConsoleUser
                };
            });

            return identity ?? IpcClientIdentity.CreateInteractiveUser();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve IPC client identity; assigning default interactive user context.");
            return IpcClientIdentity.CreateInteractiveUser();
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetActiveConsoleSessionId();

    private static async Task WriteResponseAsync(StreamWriter writer, IpcMessage response, CancellationToken cancellationToken)
        => await writer.WriteLineAsync(JsonSerializer.Serialize(response).AsMemory(), cancellationToken);

    private static NamedPipeServerStream CreateSecureServerStream()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The PrintPilotProxy management pipe is implemented for Windows only.");
        }

        try
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // LocalSystem and BuiltinAdministrators require FullControl (which includes PipeAccessRights.CreateNewInstance)
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            // Authenticated users require ReadWrite + CreateNewInstance to connect and allow subsequent instances
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                pipeSecurity);
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback to standard server stream with default system pipe security if ACL creation fails
            return new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
