using Microsoft.Extensions.Configuration;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>Settings for who may change the proxy through the management pipe.</summary>
public sealed class IpcSecurityOptions
{
    /// <summary>
    /// When true, state-changing commands need LocalSystem or an Administrator; the person at the console is no longer
    /// enough. False keeps the existing behaviour. Set with "Ipc:RequireAdministrator" in the service's appsettings.json
    /// or the environment variable Ipc__RequireAdministrator.
    /// </summary>
    public bool RequireAdministrator { get; init; }

    public static IpcSecurityOptions FromConfiguration(IConfiguration? configuration)
        => new() { RequireAdministrator = bool.TryParse(configuration?["Ipc:RequireAdministrator"], out var value) && value };
}
