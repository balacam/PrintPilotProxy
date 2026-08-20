using System;
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

public class DiscoveryServiceTests
{
    private readonly Mock<IProxyDiscoveryTransport> _mockTransport = new();
    private readonly Mock<IProxyEngine> _mockProxyEngine = new();
    private readonly Mock<IConfigurationManager> _mockConfigManager = new();
    private readonly Mock<INetworkInterfaceDiscovery> _mockNetworkDiscovery = new();
    private readonly Mock<IProxyInstanceProvider> _mockInstanceProvider = new();

    public DiscoveryServiceTests()
    {
        _mockInstanceProvider.Setup(p => p.GetInstanceId()).Returns("test-instance-12345");
        _mockProxyEngine.Setup(e => e.GetStatus()).Returns(new ProxyStatus
        {
            State = ProxyState.Running,
            ListeningAddress = "192.168.1.10:3128"
        });
        _mockConfigManager.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProxyConfiguration
            {
                Listener = new ListenerSettings { Port = 3128 }
            });
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet",
                    IsOperational = true,
                    IsPrivate = true,
                    Addresses = new List<string> { "192.168.1.10" }
                }
            });
    }

    private ProxyDiscoveryService CreateService()
    {
        return new ProxyDiscoveryService(
            _mockTransport.Object,
            _mockProxyEngine.Object,
            _mockConfigManager.Object,
            _mockNetworkDiscovery.Object,
            _mockInstanceProvider.Object,
            NullLogger<ProxyDiscoveryService>.Instance);
    }

    [Fact]
    public async Task HandleDiscoveryRequest_ValidRequest_ReturnsCorrectResponse()
    {
        var service = CreateService();
        var request = new DiscoveryRequest
        {
            Service = DiscoveryConstants.ServiceName,
            ProtocolVersion = 1,
            Request = DiscoveryConstants.DiscoverRequestAction
        };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().NotBeNull();
        response!.Service.Should().Be("PrintPilotProxy");
        response.ProtocolVersion.Should().Be(1);
        response.Version.Should().Be(typeof(ProxyDiscoveryService).Assembly.GetName().Version?.ToString(3));
        response.Host.Should().Be("192.168.1.10");
        response.ProxyPort.Should().Be(3128);
        response.InstanceId.Should().Be("test-instance-12345");
        response.Protocol.Should().Be("http-connect");
        response.AuthProtocol.Should().Be("PrintPilot-HMAC");
        response.Status.Should().Be("Running");
    }

    [Fact]
    public async Task HandleDiscoveryRequest_DynamicProxyPort_ReturnsConfiguredPort()
    {
        _mockConfigManager.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProxyConfiguration
            {
                Listener = new ListenerSettings { Port = 8080 }
            });

        var service = CreateService();
        var request = new DiscoveryRequest
        {
            Service = DiscoveryConstants.ServiceName,
            ProtocolVersion = 1,
            Request = DiscoveryConstants.DiscoverRequestAction
        };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().NotBeNull();
        response!.ProxyPort.Should().Be(8080);
    }

    [Fact]
    public async Task HandleDiscoveryRequest_MultipleInterfaces_MatchesClientSubnet()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet 1",
                    IsOperational = true,
                    IsPrivate = true,
                    Addresses = new List<string> { "10.0.0.5" }
                },
                new()
                {
                    Name = "Wi-Fi",
                    IsOperational = true,
                    IsPrivate = true,
                    Addresses = new List<string> { "192.168.1.10" }
                }
            });

        var service = CreateService();

        // Client 1 on 192.168.1.x subnet
        var req1 = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" };
        var resp1 = await service.HandleDiscoveryRequestAsync(req1, new IPEndPoint(IPAddress.Parse("192.168.1.100"), 40000));
        resp1.Should().NotBeNull();
        resp1!.Host.Should().Be("192.168.1.10");

        // Client 2 on 10.0.0.x subnet
        var req2 = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" };
        var resp2 = await service.HandleDiscoveryRequestAsync(req2, new IPEndPoint(IPAddress.Parse("10.0.0.42"), 40001));
        resp2.Should().NotBeNull();
        resp2!.Host.Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task HandleDiscoveryRequest_NeverReturns127001ForRemoteClients()
    {
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Local Area Connection",
                    IsOperational = true,
                    IsPrivate = true,
                    Addresses = new List<string> { "172.16.1.20" }
                }
            });

        var service = CreateService();
        var request = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.100.5"), 50000);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().NotBeNull();
        response!.Host.Should().NotBe("127.0.0.1");
        response.Host.Should().Be("172.16.1.20");
    }

    [Fact]
    public async Task HandleDiscoveryRequest_DynamicDhcpChange_ReflectsNewIpWithoutRestart()
    {
        var service = CreateService();
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        // Initial request
        var resp1 = await service.HandleDiscoveryRequestAsync(
            new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" },
            clientEndPoint);
        resp1!.Host.Should().Be("192.168.1.10");

        // DHCP assigned new IP address to interface
        _mockNetworkDiscovery.Setup(n => n.GetInterfacesAsync())
            .ReturnsAsync(new List<DiscoveredNetworkInterface>
            {
                new()
                {
                    Name = "Ethernet",
                    IsOperational = true,
                    IsPrivate = true,
                    Addresses = new List<string> { "192.168.1.25" }
                }
            });

        var resp2 = await service.HandleDiscoveryRequestAsync(
            new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" },
            clientEndPoint);
        resp2!.Host.Should().Be("192.168.1.25");
    }

    [Fact]
    public async Task HandleDiscoveryRequest_ProxyStopped_ReturnsStoppedStatusGracefully()
    {
        _mockProxyEngine.Setup(e => e.GetStatus()).Returns(new ProxyStatus
        {
            State = ProxyState.Stopped,
            ListeningAddress = null
        });

        var service = CreateService();
        var request = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "discover" };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().NotBeNull();
        response!.Status.Should().Be("Stopped");
        response.ProxyPort.Should().Be(3128);
    }

    [Fact]
    public async Task HandleDiscoveryRequest_UnknownService_ReturnsNull()
    {
        var service = CreateService();
        var request = new DiscoveryRequest { Service = "SomeOtherService", ProtocolVersion = 1, Request = "discover" };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().BeNull();
    }

    [Fact]
    public async Task HandleDiscoveryRequest_UnknownAction_ReturnsNull()
    {
        var service = CreateService();
        var request = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 1, Request = "shutdown" };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().BeNull();
    }

    [Fact]
    public async Task HandleDiscoveryRequest_InvalidProtocolVersion_ReturnsNull()
    {
        var service = CreateService();
        var request = new DiscoveryRequest { Service = "PrintPilotProxy", ProtocolVersion = 0, Request = "discover" };
        var clientEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 54321);

        var response = await service.HandleDiscoveryRequestAsync(request, clientEndPoint);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Lifecycle_StartAndStop_CallsTransportMethods()
    {
        var service = CreateService();

        await service.StartAsync();
        service.IsRunning.Should().BeTrue();
        _mockTransport.Verify(t => t.StartAsync(It.IsAny<Func<DiscoveryRequest, IPEndPoint, Task<DiscoveryResponse?>>>(), It.IsAny<CancellationToken>()), Times.Once);

        await service.StopAsync();
        service.IsRunning.Should().BeFalse();
        _mockTransport.Verify(t => t.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
