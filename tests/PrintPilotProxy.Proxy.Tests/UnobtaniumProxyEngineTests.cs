using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Proxy;
using Xunit;

namespace PrintPilotProxy.Proxy.Tests;

public class UnobtaniumProxyEngineTests
{
    private readonly Mock<IAccessControlList> _mockAcl = new();
    private readonly Mock<INetworkInterfaceDiscovery> _mockNetworkDiscovery = new();
    private readonly Mock<IProxyAuthenticator> _mockAuthenticator = new();

    public UnobtaniumProxyEngineTests()
    {
        _mockAcl.Setup(a => a.IsAllowed(It.IsAny<IPAddress>())).Returns(true);
        _mockAcl.Setup(a => a.IsDestinationPortAllowed(It.IsAny<int>())).Returns(true);
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet",
                    IsOperational = true,
                    Addresses = new List<string> { "127.0.0.1" }
                }
            });
    }

    [Fact]
    public void Constructor_InitializesWithStoppedState()
    {
        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockAuthenticator.Object);

        var status = engine.GetStatus();

        status.Should().NotBeNull();
        status.State.Should().Be(ProxyState.Stopped);
        status.TotalRequests.Should().Be(0);
        status.ListeningAddress.Should().BeNull();
    }

    [Fact]
    public void GetRecentRequests_InitiallyEmpty()
    {
        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockAuthenticator.Object);

        var recent = engine.GetRecentRequests(50);

        recent.Should().NotBeNull();
        recent.Should().BeEmpty();
    }
}
