using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Discovery;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Discovery;

/// <summary>PROXY-013: discovery only advertises an address and port the proxy really listens on.</summary>
public class EffectiveListenerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void ParseEffectiveListeners_NoUsableInventory_ReturnsNothing(string? input)
    {
        ProxyDiscoveryService.ParseEffectiveListeners(input).Addresses.Should().BeNull();
    }

    [Fact]
    public void ParseEffectiveListeners_ReadsAddressesAndPort()
    {
        var (addresses, port) = ProxyDiscoveryService.ParseEffectiveListeners("192.168.1.5:3128, 10.0.0.5:3128");
        addresses.Should().BeEquivalentTo(new[] { IPAddress.Parse("192.168.1.5"), IPAddress.Parse("10.0.0.5") });
        port.Should().Be(3128);
    }

    [Fact]
    public void ParseEffectiveListeners_WildcardBind_DoesNotRestrictAddresses()
    {
        ProxyDiscoveryService.ParseEffectiveListeners("0.0.0.0:3128").Addresses.Should().BeNull();
    }

    [Fact]
    public async Task Discovery_AdvertisesTheListeningAddress_NotAnotherLanAddress()
    {
        var engine = new Mock<IProxyEngine>();
        engine.Setup(e => e.GetStatus()).Returns(new ProxyStatus { State = ProxyState.Running, ListeningAddress = "127.0.0.1:4444" });
        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProxyConfiguration { Listener = new ListenerSettings { Port = 3128 } });
        var network = new Mock<INetworkInterfaceDiscovery>();
        network.Setup(n => n.GetInterfacesAsync()).ReturnsAsync(new List<DiscoveredNetworkInterface>
        {
            new() { Name = "Ethernet", IsOperational = true, IsPrivate = true, Addresses = new List<string> { "192.168.1.10" } }
        });
        var instance = new Mock<IProxyInstanceProvider>();
        instance.Setup(p => p.GetInstanceId()).Returns("id");
        var service = new ProxyDiscoveryService(new Mock<IProxyDiscoveryTransport>().Object, engine.Object, config.Object,
            network.Object, instance.Object, NullLogger<ProxyDiscoveryService>.Instance);

        var response = await service.HandleDiscoveryRequestAsync(
            new DiscoveryRequest { Service = DiscoveryConstants.ServiceName, ProtocolVersion = 1, Request = DiscoveryConstants.DiscoverRequestAction },
            new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321));

        response.Should().NotBeNull();
        response!.Host.Should().Be("127.0.0.1");
        response.ProxyPort.Should().Be(4444);
    }
}
