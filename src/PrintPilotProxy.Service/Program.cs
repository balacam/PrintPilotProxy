using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PrintPilotProxy.Service;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(@"C:\ProgramData\PrintPilotProxy\logs\service.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting PrintPilotProxy Service...");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Service terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "PrintPilotProxy";
            })
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                PrintPilotProxy.Infrastructure.InfrastructureServiceExtensions.AddInfrastructureServices(services);
                PrintPilotProxy.Proxy.ProxyServiceExtensions.AddProxyServices(services);
                services.AddHostedService<ProxyWorker>();
            });
}
