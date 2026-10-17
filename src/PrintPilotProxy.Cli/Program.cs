using System;
using System.IO;
using System.Text.Json;
using PrintPilotProxy.Infrastructure;
using PrintPilotProxy.Infrastructure.Ipc;
using PrintPilotProxy.Core.Validation;
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
                await SendManagementCommandAsync(IpcMessageTypes.StopProxy);
                break;
            case "status":
                await SendManagementCommandAsync(IpcMessageTypes.GetStatus);
                break;
            case "validate":
                ValidateConfiguration(args.Length > 1 ? args[1] : @"C:\ProgramData\PrintPilotProxy\config.json");
                break;
            case "version":
                Console.WriteLine($"PrintPilotProxy Version {typeof(Program).Assembly.GetName().Version}");
                break;
            default:
                PrintHelp();
                break;
        }
    }

    private static async Task SendManagementCommandAsync(string type)
    {
        try
        {
            await using var client = new NamedPipeIpcClient();
            var response = await client.SendAsync(new IpcMessage { Type = type });
            Console.WriteLine(response.Payload ?? response.Type);
            if (response.Type == IpcMessageTypes.Error) Environment.ExitCode = 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
    }

    private static void ValidateConfiguration(string path)
    {
        try
        {
            var configuration = JsonSerializer.Deserialize<ProxyConfiguration>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty configuration.");
            var errors = ConfigurationValidator.Validate(configuration);
            foreach (var error in errors) Console.Error.WriteLine(error);
            Environment.ExitCode = errors.Count == 0 ? 0 : 1;
            if (errors.Count == 0) Console.WriteLine("Configuration is valid.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
    }

    private static async Task StartProxyHeadlessAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddInfrastructureServices();
        services.AddProxyServices();

        var serviceProvider = services.BuildServiceProvider();
        var proxyEngine = serviceProvider.GetRequiredService<IProxyEngine>();

        var config = new ProxyConfiguration
        {
            // SpecificAddress is required: with the default Auto mode the engine would ignore ListenAddress and could bind to LAN addresses.
            Listener = new ListenerSettings { Mode = ListenerMode.SpecificAddress, ListenAddress = System.Net.IPAddress.Loopback.ToString(), Port = 8080 },
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
        Console.WriteLine("  start    Run a standalone engine on 127.0.0.1:8080 in this console (Ctrl+C to stop)");
        Console.WriteLine("  stop     Stop the proxy engine of the installed Windows service (not a 'start' instance)");
        Console.WriteLine("  status   Query the installed Windows service (not a 'start' instance)");
        Console.WriteLine("  validate [path] Validate a configuration file without changing it");
        Console.WriteLine("  version  Print version information");
        Console.WriteLine("  help     Show this help text");
    }
}

