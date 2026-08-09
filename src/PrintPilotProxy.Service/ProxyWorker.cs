using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Service;

public class ProxyWorker : BackgroundService
{
    private readonly ILogger<ProxyWorker> _logger;
    private readonly IProxyEngine _proxyEngine;
    private readonly PrintPilotProxy.Core.Interfaces.IConfigurationManager _configManager;

    public ProxyWorker(ILogger<ProxyWorker> logger, IProxyEngine proxyEngine, PrintPilotProxy.Core.Interfaces.IConfigurationManager configManager)
    {
        _logger = logger;
        _proxyEngine = proxyEngine;
        _configManager = configManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProxyWorker is starting.");
        
        var config = await _configManager.LoadAsync(stoppingToken);
        
        _proxyEngine.RequestProcessed += ProxyEngine_RequestProcessed;

        try
        {
            await _proxyEngine.StartAsync(config, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            _proxyEngine.RequestProcessed -= ProxyEngine_RequestProcessed;
            await _proxyEngine.StopAsync(stoppingToken);
        }

        _logger.LogInformation("ProxyWorker is stopping.");
    }

    private void ProxyEngine_RequestProcessed(object? sender, ProxyRequestEntry e)
    {
        _logger.LogInformation("Request: {ClientIp} {Method} {Destination} - {StatusCode}", e.ClientIp, e.Method, e.Destination, e.StatusCode);
    }
}
