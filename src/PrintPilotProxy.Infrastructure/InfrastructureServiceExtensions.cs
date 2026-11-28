using System;
using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Security;
using PrintPilotProxy.Infrastructure.Configuration;
using PrintPilotProxy.Infrastructure.Diagnostics;
using PrintPilotProxy.Infrastructure.Discovery;
using PrintPilotProxy.Infrastructure.Ipc;
using PrintPilotProxy.Infrastructure.Platform;
using PrintPilotProxy.Infrastructure.Security;

namespace PrintPilotProxy.Infrastructure
{
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddSingleton<IConfigurationManager, JsonConfigurationManager>();

            // The network classes are plain .NET and work everywhere; the Windows prefix is historical.
            services.AddTransient<IPlatformNetworkManager, WindowsNetworkManager>();
            services.AddSingleton<INetworkInterfaceDiscovery, WindowsNetworkDiscovery>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IPlatformPathProvider, WindowsPathProvider>();
                services.AddTransient<IPlatformFirewallManager, WindowsFirewallManager>();
                services.AddTransient<IPlatformServiceManager, WindowsServiceManager>();
                services.AddSingleton<IDataProtector, DpapiDataProtector>();
            }
            else
            {
                services.AddSingleton<IPlatformPathProvider, UnixPathProvider>();
                services.AddTransient<IPlatformFirewallManager, UnsupportedFirewallManager>();
                services.AddTransient<IPlatformServiceManager, LinuxServiceManager>();
                services.AddSingleton<IDataProtector, KeyFileDataProtector>();
            }

            services.AddTransient<IDiagnosticsRunner, DiagnosticsRunner>();
            services.AddTransient<IInternetConnectivityTester, InternetConnectivityTester>();
            services.AddTransient<ISecurityAuditor, SecurityAuditor>();
            services.AddSingleton<IIpcServer, NamedPipeIpcServer>();
            services.AddSingleton<IIpcClient, NamedPipeIpcClient>();
            services.AddSingleton<IpcSecurityOptions>(sp =>
                IpcSecurityOptions.FromConfiguration(sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>()));
            services.AddSingleton<IIpcSecurityValidator, IpcSecurityValidator>();

            services.AddSingleton<IProxyInstanceProvider, PersistentProxyInstanceProvider>();
            services.AddSingleton<IDiscoveryRateLimiter, DiscoveryRateLimiter>();
            services.AddSingleton<IProxyDiscoveryTransport, UdpBroadcastDiscoveryTransport>();
            services.AddSingleton<IProxyDiscoveryService, ProxyDiscoveryService>();
            services.AddSingleton<IProxyAuthenticator, PrintPilotHmacAuthenticator>();

            return services;
        }
    }
}
