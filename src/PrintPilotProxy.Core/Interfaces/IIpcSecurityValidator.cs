using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Validates authorization for local IPC command execution based on the client's
/// Windows security identity and command privilege level.
/// </summary>
public interface IIpcSecurityValidator
{
    /// <summary>
    /// Evaluates whether the IPC request is permitted for the given caller identity.
    /// Returns true if authorized; false if access is denied.
    /// </summary>
    bool IsAuthorized(IpcClientIdentity clientIdentity, string commandType, out string? failureReason);

    /// <summary>
    /// Determines whether the specified command is classified as read-only.
    /// </summary>
    bool IsReadOnlyCommand(string commandType);

    /// <summary>
    /// Determines whether the specified command is classified as privileged/mutating.
    /// </summary>
    bool IsPrivilegedCommand(string commandType);
}
