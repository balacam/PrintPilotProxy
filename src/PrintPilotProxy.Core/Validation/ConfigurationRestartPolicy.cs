using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Validation;

/// <summary>
/// Decides whether a configuration change only takes effect after the proxy engine restarts.
/// The engine reads its configuration once in StartAsync, so every setting it consults while
/// serving requests must be listed here (PROXY-003).
/// </summary>
public static class ConfigurationRestartPolicy
{
    public static bool RequiresProxyRestart(ProxyConfiguration before, ProxyConfiguration after)
        => before.Listener.Mode != after.Listener.Mode ||
           before.Listener.ListenAddress != after.Listener.ListenAddress ||
           before.Listener.AdapterName != after.Listener.AdapterName ||
           before.Listener.Port != after.Listener.Port ||
           before.Listener.MaxConnections != after.Listener.MaxConnections ||
           before.Listener.ConnectionTimeoutSeconds != after.Listener.ConnectionTimeoutSeconds ||
           before.ClientAccess.Mode != after.ClientAccess.Mode ||
           !before.ClientAccess.AllowedClients.Select(client => (client.Id, client.Name, client.IpOrCidr, client.Enabled))
               .SequenceEqual(after.ClientAccess.AllowedClients.Select(client => (client.Id, client.Name, client.IpOrCidr, client.Enabled))) ||
           before.Security.DestinationPortRestrictionsEnabled != after.Security.DestinationPortRestrictionsEnabled ||
           !before.Security.AllowedDestinationPorts.SequenceEqual(after.Security.AllowedDestinationPorts) ||
           before.Security.RequireAuthentication != after.Security.RequireAuthentication ||
           before.UpstreamProxy.Mode != after.UpstreamProxy.Mode ||
           before.UpstreamProxy.Host != after.UpstreamProxy.Host ||
           before.UpstreamProxy.Port != after.UpstreamProxy.Port ||
           before.UpstreamProxy.Username != after.UpstreamProxy.Username ||
           before.UpstreamProxy.ProtectedPassword != after.UpstreamProxy.ProtectedPassword;
}