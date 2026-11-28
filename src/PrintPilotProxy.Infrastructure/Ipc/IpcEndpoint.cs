using System;
using System.IO;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>
/// Where the management channel lives. Windows uses the fixed pipe name (unchanged, so existing clients keep
/// working). Elsewhere .NET maps a named pipe to a Unix domain socket; the socket gets an explicit path under
/// <c>/run/printpilotproxy</c> so a sandboxed service (systemd <c>PrivateTmp</c>) and the CLI see the same file.
/// </summary>
public static class IpcEndpoint
{
    public const string SocketOverrideVariable = "PRINTPILOTPROXY_IPC_SOCKET";
    private const string RuntimeDirectory = "/run/printpilotproxy";

    /// <summary>Pipe name on Windows, absolute socket path on Unix.</summary>
    public static string Name => OperatingSystem.IsWindows() ? NamedPipeIpcServer.PipeName : ResolveUnixSocketPath();

    internal static string ResolveUnixSocketPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(SocketOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Directory.Exists(RuntimeDirectory)
            ? Path.Combine(RuntimeDirectory, "ipc.sock")
            : Path.Combine(Path.GetTempPath(), "printpilotproxy.sock");
    }
}
