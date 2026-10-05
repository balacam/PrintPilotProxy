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

/// <summary>
/// MaxConnections is advisory (see <see cref="UnobtaniumProxyEngine"/>): the engine's connection count includes
/// half-open and lingering connections, so refusing on it would let one LAN host lock every client out.
/// </summary>
public class ConnectionLimitTests
{
    [Theory]
    [InlineData(1, 1, false)]   // exactly MaxConnections clients are served
    [InlineData(2, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1000, 1000, false)]
    [InlineData(1001, 1000, true)]
    [InlineData(5, 0, false)]   // a non-positive limit means "no limit configured", never "refuse everything"
    public void IsExceeded_ComparesOpenConnectionsWithTheLimit(int current, int max, bool expected)
    {
        ConnectionLimit.IsExceeded(current, max).Should().Be(expected);
    }

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

    private static ProxyConfiguration Config(int proxyPort, int maxConnections) => new()
    {
        Listener = new ListenerSettings
        {
            Mode = ListenerMode.SpecificAddress,
            ListenAddress = "127.0.0.1",
            Port = proxyPort,
            MaxConnections = maxConnections,
            ConnectionTimeoutSeconds = 3
        }
    };

    /// <summary>Tiny origin server: answers every request with "200 ok".</summary>
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
                        var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct);
                    }
                }, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private static async Task<string> RequestThroughProxyAsync(int proxyPort, int originPort, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort, ct);
        var stream = client.GetStream();
        var request = $"GET http://127.0.0.1:{originPort}/ HTTP/1.1\r\nHost: 127.0.0.1:{originPort}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), ct);

        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, ct);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    [Fact]
    public async Task EngineStillServesWhenTheLimitIsConfigured()
    {
        const int proxyPort = 18153, originPort = 18154;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var origin = new TcpListener(IPAddress.Loopback, originPort);
        origin.Start();
        var originTask = RunOriginAsync(origin, cts.Token);
        var engine = NewEngine();
        await engine.StartAsync(Config(proxyPort, maxConnections: 1000));
        try
        {
            (await RequestThroughProxyAsync(proxyPort, originPort, cts.Token)).Should().StartWith("HTTP/1.1 200");
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
