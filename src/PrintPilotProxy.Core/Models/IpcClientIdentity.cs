using System.Security.Principal;

namespace PrintPilotProxy.Core.Models;

/// <summary>
/// Represents the validated Windows security identity of an IPC client connection.
/// </summary>
public sealed class IpcClientIdentity
{
    /// <summary>Windows User SID of the calling process.</summary>
    public string UserSid { get; init; } = string.Empty;

    /// <summary>Account display or logon name if available.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Windows session ID of the caller token.</summary>
    public int SessionId { get; init; }

    /// <summary>Indicates whether the caller runs as LocalSystem (S-1-5-18).</summary>
    public bool IsLocalSystem { get; init; }

    /// <summary>Indicates whether the caller runs with Windows Administrator privileges.</summary>
    public bool IsAdministrator { get; init; }

    /// <summary>Indicates whether the caller token belongs to an interactive user logon.</summary>
    public bool IsInteractiveUser { get; init; }

    /// <summary>Indicates whether the caller SID matches the active console desktop session user.</summary>
    public bool IsActiveConsoleUser { get; init; }

    /// <summary>Returns a default anonymous/unknown identity for unauthenticated or test contexts.</summary>
    public static IpcClientIdentity Unknown { get; } = new()
    {
        UserSid = "S-1-0-0",
        Name = "Unknown",
        SessionId = -1,
        IsLocalSystem = false,
        IsAdministrator = false,
        IsInteractiveUser = false,
        IsActiveConsoleUser = false
    };

    /// <summary>Returns a pre-configured identity representing LocalSystem.</summary>
    public static IpcClientIdentity LocalSystem { get; } = new()
    {
        UserSid = "S-1-5-18",
        Name = @"NT AUTHORITY\SYSTEM",
        SessionId = 0,
        IsLocalSystem = true,
        IsAdministrator = true,
        IsInteractiveUser = false,
        IsActiveConsoleUser = true
    };

    /// <summary>Returns a pre-configured identity representing an Administrator.</summary>
    public static IpcClientIdentity Administrator { get; } = new()
    {
        UserSid = "S-1-5-32-544",
        Name = @"BUILTIN\Administrators",
        SessionId = 1,
        IsLocalSystem = false,
        IsAdministrator = true,
        IsInteractiveUser = true,
        IsActiveConsoleUser = true
    };

    /// <summary>Returns a identity representing the active interactive desktop user.</summary>
    public static IpcClientIdentity CreateInteractiveUser(string userSid = "S-1-5-21-1000", int sessionId = 1) => new()
    {
        UserSid = userSid,
        Name = "DesktopUser",
        SessionId = sessionId,
        IsLocalSystem = false,
        IsAdministrator = false,
        IsInteractiveUser = true,
        IsActiveConsoleUser = true
    };

    /// <summary>Returns a identity representing an unauthorized local user (User B).</summary>
    public static IpcClientIdentity CreateUnauthorizedUser(string userSid = "S-1-5-21-9999", int sessionId = 2) => new()
    {
        UserSid = userSid,
        Name = "OtherUser",
        SessionId = sessionId,
        IsLocalSystem = false,
        IsAdministrator = false,
        IsInteractiveUser = false,
        IsActiveConsoleUser = false
    };
}
