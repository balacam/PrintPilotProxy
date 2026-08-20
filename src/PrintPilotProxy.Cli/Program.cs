using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Proxy;

namespace PrintPilotProxy.Cli;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "start":
                Console.WriteLine("Starting PrintPilotProxy in headless mode...");
                await StartProxyHeadlessAsync();
                break;
            case "stop":
                Console.WriteLine("Stopping PrintPilotProxy...");
                break;
            case "status":
                Console.WriteLine("Querying status...");
                break;
            case "validate":
                Console.WriteLine("Validating configuration...");
                break;
            case "version":
                Console.WriteLine("PrintPilotProxy Version 0.5.0");
                break;
            default:
                PrintHelp();
                break;
        }
    }

    private static async Task StartProxyHeadlessAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddProxyServices();

        var serviceProvider = services.BuildServiceProvider();
        var proxyEngine = serviceProvider.GetRequiredService<IProxyEngine>();

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings { ListenAddress = System.Net.IPAddress.Loopback.ToString(), Port = 8080 },
            Security = new SecuritySettings 
            { 
                DestinationPortRestrictionsEnabled = true,
                AllowedDestinationPorts = new System.Collections.Generic.List<int> { 80, 443 }
            },
            ClientAccess = new ClientAccessSettings
            {
                Mode = ClientAccessMode.AllowList,
                AllowedClients = new System.Collections.Generic.List<AllowedClient> 
                {
                    new AllowedClient { IpOrCidr = System.Net.IPAddress.Loopback.ToString(), Enabled = true, Name = "Localhost" }
                }
            }
        };
        
        var acl = serviceProvider.GetRequiredService<IAccessControlList>();
        acl.Refresh(config);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => 
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await proxyEngine.StartAsync(config, cts.Token);
        Console.WriteLine($"Proxy running on {config.Listener.ListenAddress}:{config.Listener.Port}. Press Ctrl+C to stop.");

        try 
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
        }

        await proxyEngine.StopAsync();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PrintPilotProxy CLI");
        Console.WriteLine("Usage: PrintPilotProxy.Cli [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  start    Start the proxy engine directly (headless mode)");
        Console.WriteLine("  stop     Stop the running proxy service");
        Console.WriteLine("  status   Query the status of the running proxy service");
        Console.WriteLine("  validate Load and validate the configuration file");
        Console.WriteLine("  version  Print version information");
        Console.WriteLine("  help     Show this help text");
    }
}

