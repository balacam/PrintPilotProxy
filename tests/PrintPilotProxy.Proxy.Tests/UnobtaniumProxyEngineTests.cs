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
    private readonly Mock<IDataProtector> _mockDataProtector = new();

    public UnobtaniumProxyEngineTests()
    {
        _mockAcl.Setup(a => a.IsAllowed(It.IsAny<IPAddress>())).Returns(true);
        _mockAcl.Setup(a => a.IsDestinationPortAllowed(It.IsAny<int>())).Returns(true);
        _mockDataProtector.Setup(d => d.Unprotect(It.IsAny<string>())).Returns("unprotected");
        _mockDataProtector.Setup(d => d.Protect(It.IsAny<string>())).Returns("protected");
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
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var status = engine.GetStatus();

        status.Should().NotBeNull();
        status.State.Should().Be(ProxyState.Stopped);
        status.TotalRequests.Should().Be(0);
        status.ListeningAddress.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_SpecificAddress_MatchingIpv4_StartsSuccessfully()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet1",
                    IsOperational = true,
                    Addresses = new List<string> { "127.0.0.1" }
                }
            });

        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings
            {
                Mode = ListenerMode.SpecificAddress,
                ListenAddress = "127.0.0.1",
                Port = 18128
            }
        };

        await engine.StartAsync(config);
        var status = engine.GetStatus();
        status.State.Should().Be(ProxyState.Running);
        status.ListeningAddress.Should().Contain("127.0.0.1:18128");

        await engine.StopAsync();
        engine.GetStatus().State.Should().Be(ProxyState.Stopped);
    }

    [Fact]
    public async Task StartAsync_SpecificAddress_NonMatchingIpv4_ThrowsAndDoesNotMaskWithProxyIsNotRunning()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet1",
                    IsOperational = true,
                    Addresses = new List<string> { "192.168.10.10" }
                }
            });

        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings
            {
                Mode = ListenerMode.SpecificAddress,
                ListenAddress = "192.168.10.20", // Different IP
                Port = 18129
            }
        };

        var act = async () => await engine.StartAsync(config);

        // Crucial: Must throw the primary InvalidOperationException and NOT "Proxy is not running."
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("The configured listener address is not assigned to this computer.");
        exception.Which.Message.Should().NotContain("Proxy is not running");
    }

    [Fact]
    public async Task StartAsync_SpecificAddress_InvalidAddressStringInList_DoesNotPreventValidMatch()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet1",
                    IsOperational = true,
                    Addresses = new List<string> { "not-an-ip", "127.0.0.1" }
                }
            });

        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings
            {
                Mode = ListenerMode.SpecificAddress,
                ListenAddress = "127.0.0.1",
                Port = 18130
            }
        };

        await engine.StartAsync(config);
        engine.GetStatus().State.Should().Be(ProxyState.Running);

        await engine.StopAsync();
    }

    [Fact]
    public async Task StartAsync_SpecificAddress_MultipleAddresses_MatchesCorrectTarget()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet1",
                    IsOperational = true,
                    Addresses = new List<string> { "192.168.10.10", "127.0.0.1" }
                }
            });

        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings
            {
                Mode = ListenerMode.SpecificAddress,
                ListenAddress = "127.0.0.1",
                Port = 18131
            }
        };

        await engine.StartAsync(config);
        engine.GetStatus().State.Should().Be(ProxyState.Running);

        await engine.StopAsync();
    }

    [Fact]
    public async Task StartAsync_SpecificAddress_NoMatchingAddress_ThrowsClearError()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet1",
                    IsOperational = true,
                    Addresses = new List<string> { "10.0.0.1" }
                }
            });

        var engine = new UnobtaniumProxyEngine(
            NullLogger<UnobtaniumProxyEngine>.Instance,
            _mockAcl.Object,
            _mockNetworkDiscovery.Object,
            _mockDataProtector.Object,
            _mockAuthenticator.Object);

        var config = new ProxyConfiguration
        {
            Listener = new ListenerSettings
            {
                Mode = ListenerMode.SpecificAddress,
                ListenAddress = "192.168.1.1",
                Port = 18132
            }
        };

        var act = async () => await engine.StartAsync(config);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The configured listener address is not assigned to this computer.");
    }
}
