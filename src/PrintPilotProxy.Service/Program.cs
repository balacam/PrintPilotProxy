using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PrintPilotProxy.Service;

public class Program
{
    public static void Main(string[] args)
    {
        // The log sink honours the retention, size and level settings from config.json (read once at start).
        var loggingSettings = ReadLoggingSettings(@"C:\ProgramData\PrintPilotProxy\config.json");
        var minimumLevel = new LoggingLevelSwitch(Enum.Parse<LogEventLevel>(LoggingPolicy.NormalizeMinimumLevel(loggingSettings)));
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(minimumLevel)
            .WriteTo.Console()
            .WriteTo.File(
                @"C:\ProgramData\PrintPilotProxy\logs\service.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: LoggingPolicy.RetainedFileCount(loggingSettings),
                fileSizeLimitBytes: LoggingPolicy.FileSizeLimitBytes(loggingSettings),
                rollOnFileSizeLimit: true)
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

    private static LoggingSettings? ReadLoggingSettings(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("logging", out var element) && element.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<LoggingSettings>(element.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }
        catch
        {
            // A missing or unreadable file keeps the built-in defaults.
            return null;
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
