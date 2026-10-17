using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Proxy;
using Xunit;

namespace PrintPilotProxy.Proxy.Tests;

/// <summary>PROXY-012: a request is recorded once, with the status and size the origin really returned.</summary>
public class RequestMetricsTests
{
    private static UnobtaniumProxyEngine NewEngine()
    {
        var acl = new Mock<IAccessControlList>();
        acl.Setup(a => a.IsAllowed(It.IsAny<IPAddress>())).Returns(true);
        acl.Setup(a => a.IsDestinationPortAllowed(It.IsAny<int>())).Returns(true);
        var discovery = new Mock<INetworkInterfaceDiscovery>();
        discovery.Setup(n => n.GetInterfacesAsync()).ReturnsAsync(new List<DiscoveredNetworkInterface>
        {
            new() { Name = "Loopback", IsOperational = true, Addresses = new List<string> { "127.0.0.1" } }
        });
        var protector = new Mock<IDataProtector>();
        protector.Setup(d => d.Unprotect(It.IsAny<string>())).Returns("x");
        return new UnobtaniumProxyEngine(NullLogger<UnobtaniumProxyEngine>.Instance, acl.Object, discovery.Object, protector.Object);
    }

    private static ProxyConfiguration Config(int proxyPort) => new()
    {
        Listener = new ListenerSettings
        {
            Mode = ListenerMode.SpecificAddress,
            ListenAddress = "127.0.0.1",
            Port = proxyPort,
            MaxConnections = 1000,
            ConnectionTimeoutSeconds = 3
        }
    };

    private static async Task RunOriginAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        var buffer = new byte[4096];
                        await stream.ReadAsync(buffer, ct);
                        var response = "HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\nConnection: close\r\n\r\nnope";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct);
                    }
                }, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    [Fact]
    public async Task HttpRequest_IsLoggedOnceWithTheOriginsStatusAndSize()
    {
        const int proxyPort = 18163, originPort = 18164;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var origin = new TcpListener(IPAddress.Loopback, originPort);
        origin.Start();
        var originTask = RunOriginAsync(origin, cts.Token);
        var engine = NewEngine();
        await engine.StartAsync(Config(proxyPort));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, proxyPort, cts.Token);
            var stream = client.GetStream();
            var request = $"GET http://127.0.0.1:{originPort}/ HTTP/1.1\r\nHost: 127.0.0.1:{originPort}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cts.Token);
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer, cts.Token);
            Encoding.ASCII.GetString(buffer, 0, read).Should().StartWith("HTTP/1.1 404");

            for (var i = 0; i < 30 && engine.GetRecentRequests().Count == 0; i++)
            {
                await Task.Delay(100, cts.Token);
            }

            var entries = engine.GetRecentRequests();
            entries.Should().ContainSingle();
            entries[0].StatusCode.Should().Be(404);
            entries[0].BytesTransferred.Should().Be(4);

            var status = engine.GetStatus();
            status.TotalRequests.Should().Be(1);
            status.TotalErrors.Should().Be(1);
            status.TotalBytesTransferred.Should().Be(4);
        }
        finally
        {
            await engine.StopAsync();
            cts.Cancel();
            origin.Stop();
            await originTask;
        }
    }
}
