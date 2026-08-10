using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>
/// Authorizes local IPC commands based on caller identity, session attributes,
/// and command privilege classification.
/// </summary>
public sealed class IpcSecurityValidator : IIpcSecurityValidator
{
    private static readonly HashSet<string> ReadOnlyCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        IpcMessageTypes.GetStatus,
        IpcMessageTypes.GetConfiguration,
        IpcMessageTypes.GetRecentRequests,
        IpcMessageTypes.GetNetworkInterfaces,
        IpcMessageTypes.GetFirewallStatus,
        IpcMessageTypes.RunDiagnostics,
        IpcMessageTypes.GetSecurityAudit
    };

    private static readonly HashSet<string> PrivilegedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        IpcMessageTypes.StartProxy,
        IpcMessageTypes.StopProxy,
        IpcMessageTypes.RestartProxy,
        IpcMessageTypes.UpdateConfiguration,
        IpcMessageTypes.ApplyFirewallRule,
        IpcMessageTypes.RemoveFirewallRule
    };

    private readonly ILogger<IpcSecurityValidator> _logger;

    public IpcSecurityValidator(ILogger<IpcSecurityValidator>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<IpcSecurityValidator>.Instance;
    }

    public bool IsReadOnlyCommand(string commandType)
        => ReadOnlyCommands.Contains(commandType);

    public bool IsPrivilegedCommand(string commandType)
        => PrivilegedCommands.Contains(commandType);

    public bool IsAuthorized(IpcClientIdentity clientIdentity, string commandType, out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(commandType))
        {
            failureReason = "IPC command type cannot be empty.";
            return false;
        }

        // Read-only inspection commands are permitted for authenticated local desktop callers.
        if (IsReadOnlyCommand(commandType))
        {
            return true;
        }

        if (!IsPrivilegedCommand(commandType))
        {
            failureReason = $"Unknown IPC command '{commandType}'.";
            return false;
        }

        // Privileged commands require LocalSystem, Administrator privileges, or the Active Interactive Console Desktop User.
        if (clientIdentity.IsLocalSystem)
        {
            _logger.LogDebug("IPC command '{Command}' authorized for LocalSystem caller.", commandType);
            return true;
        }

        if (clientIdentity.IsAdministrator)
        {
            _logger.LogDebug("IPC command '{Command}' authorized for Administrator caller '{User}'.", commandType, clientIdentity.Name);
            return true;
        }

        if (clientIdentity.IsActiveConsoleUser)
        {
            _logger.LogDebug("IPC command '{Command}' authorized for active desktop session user '{User}'.", commandType, clientIdentity.Name);
            return true;
        }

        // Deny access for unauthorized local users attempting privileged operations.
        failureReason = $"Access denied: User '{clientIdentity.Name}' ({clientIdentity.UserSid}) is not authorized to invoke privileged command '{commandType}'.";
        _logger.LogWarning("Security violation: Denied privileged IPC command '{Command}' to caller '{User}' (SID: {UserSid}, Session: {SessionId}).",
            commandType, clientIdentity.Name, clientIdentity.UserSid, clientIdentity.SessionId);
        return false;
    }
}
