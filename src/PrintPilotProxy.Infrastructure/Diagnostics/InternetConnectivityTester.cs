using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Diagnostics;

public class InternetConnectivityTester : IInternetConnectivityTester
{
    private readonly IDataProtector _dataProtector;
    
    public string TargetHost { get; set; } = "github.com";
    public int TargetPort { get; set; } = 443;

    public InternetConnectivityTester(IDataProtector dataProtector)
    {
        _dataProtector = dataProtector;
    }

    public async Task<string> RunTestAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (configuration.UpstreamProxy.Mode == UpstreamProxyMode.Manual && !string.IsNullOrWhiteSpace(configuration.UpstreamProxy.Host))
            {
                // Test via Upstream Proxy
                return await TestViaProxyAsync(
                    configuration.UpstreamProxy.Host, 
                    configuration.UpstreamProxy.Port, 
                    configuration.UpstreamProxy.Username, 
                    _dataProtector.Unprotect(configuration.UpstreamProxy.ProtectedPassword ?? string.Empty),
                    TargetHost, 
                    TargetPort, 
                    cancellationToken);
            }
            else
            {
                // Test Direct
                using var client = new TcpClient();
                await client.ConnectAsync(TargetHost, TargetPort, cancellationToken);
                return "ConnectedDirect";
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.HostNotFound)
                return "DNSFailure";
            return "TargetUnreachable";
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                return "Timeout";
            return "UnknownFailure";
        }
    }

    private async Task<string> TestViaProxyAsync(string proxyHost, int proxyPort, string? username, string? password, string targetHost, int targetPort, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(proxyHost, proxyPort, cancellationToken);
        }
        catch (SocketException)
        {
            return "UpstreamUnavailable";
        }

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        var connectRequest = $"CONNECT {targetHost}:{targetPort} HTTP/1.1\r\nHost: {targetHost}:{targetPort}\r\n";
        
        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            connectRequest += $"Proxy-Authorization: Basic {auth}\r\n";
        }
        
        connectRequest += "\r\n";
        
        await writer.WriteAsync(connectRequest.AsMemory(), cancellationToken);

        var responseLine = await reader.ReadLineAsync(cancellationToken);
        if (responseLine == null)
            return "UpstreamUnavailable";

        if (responseLine.Contains("200"))
        {
            return "ConnectedViaUpstream";
        }
        else if (responseLine.Contains("407"))
        {
            return "UpstreamAuthenticationFailed";
        }
        else
        {
            return "TargetUnreachable"; // Could be 502/503 from upstream
        }
    }
}
