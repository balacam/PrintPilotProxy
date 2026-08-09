using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Proxy;

/// <summary>
/// Extension methods for registering proxy services.
/// </summary>
public static class ProxyServiceExtensions
{
    /// <summary>
    /// Adds proxy-related services to the service collection.
    /// </summary>
    public static IServiceCollection AddProxyServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccessControlList, AccessControlList>();
        services.AddSingleton<IProxyEngine, UnobtaniumProxyEngine>();

        return services;
    }
}
