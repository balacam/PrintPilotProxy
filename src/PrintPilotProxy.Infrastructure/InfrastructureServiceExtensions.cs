using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Security;
using PrintPilotProxy.Infrastructure.Configuration;
using PrintPilotProxy.Infrastructure.Diagnostics;
using PrintPilotProxy.Infrastructure.Ipc;
using PrintPilotProxy.Infrastructure.Platform;

namespace PrintPilotProxy.Infrastructure
{
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddSingleton<IPlatformPathProvider, WindowsPathProvider>();
            services.AddSingleton<IConfigurationManager, JsonConfigurationManager>();

            services.AddTransient<IPlatformNetworkManager, WindowsNetworkManager>();
            services.AddTransient<IPlatformFirewallManager, WindowsFirewallManager>();
            services.AddTransient<IPlatformServiceManager, WindowsServiceManager>();
            services.AddSingleton<INetworkInterfaceDiscovery, WindowsNetworkDiscovery>();

            services.AddTransient<IDiagnosticsRunner, DiagnosticsRunner>();
            services.AddTransient<ISecurityAuditor, SecurityAuditor>();

            services.AddSingleton<IIpcServer, NamedPipeIpcServer>();
            services.AddTransient<IIpcClient, NamedPipeIpcClient>();
            services.AddSingleton<IIpcSecurityValidator, IpcSecurityValidator>();

            return services;
        }
    }
}
