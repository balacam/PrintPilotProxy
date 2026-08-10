using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using Xunit;

namespace PrintPilotProxy.App.Tests;

/// <summary>
/// Tests for NetworkSettingsViewModel validation logic.
/// No real network, no real IPC — all via mocks.
/// </summary>
public class NetworkSettingsViewModelTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IpcClientService BuildIpcService(ProxyConfiguration? configToReturn = null)
    {
        var mockClient = new Mock<IIpcClient>();
        mockClient.Setup(c => c.IsConnected).Returns(true);
        mockClient.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // GetConfiguration response
        var cfg = configToReturn ?? DefaultConfig();
        var cfgJson = System.Text.Json.JsonSerializer.Serialize(cfg,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        mockClient.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.GetConfiguration),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.ConfigurationResponse,
                Payload = cfgJson
            });

        // GetNetworkInterfaces response
        var ifaces = new List<DiscoveredNetworkInterface>
        {
            new DiscoveredNetworkInterface
            {
                Name = "Ethernet", IsPrivate = true, IsOperational = true,
                Addresses = new List<IPAddress> { IPAddress.Parse("192.168.1.100") }
            }
        };
        var ifacesJson = System.Text.Json.JsonSerializer.Serialize(ifaces,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        mockClient.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.GetNetworkInterfaces),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.NetworkInterfacesResponse,
                Payload = ifacesJson
            });

        // UpdateConfiguration success
        mockClient.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.UpdateConfiguration),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.Success,
                Payload = "Configuration updated and applied."
            });

        return new IpcClientService(mockClient.Object);
    }

    private static ProxyConfiguration DefaultConfig() => new()
    {
        Listener = new ListenerSettings { Mode = ListenerMode.Auto, Port = 3128 },
        ClientAccess = new ClientAccessSettings { Mode = ClientAccessMode.AllowAll },
        Security = new SecuritySettings()
    };

    // ─── Port validation ──────────────────────────────────────────────────────

    [Fact]
    public void ProxyPort_ZeroIsInvalid()
    {
        // Just test the validation logic directly via NetworkValidator
        var valid = PrintPilotProxy.Core.Validation.NetworkValidator.IsValidPort(0);
        valid.Should().BeFalse();
    }

    [Fact]
    public void ProxyPort_65535IsValid()
    {
        var valid = PrintPilotProxy.Core.Validation.NetworkValidator.IsValidPort(65535);
        valid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3128)]
    [InlineData(8080)]
    [InlineData(65535)]
    public void ProxyPort_ValidPorts(int port)
    {
        PrintPilotProxy.Core.Validation.NetworkValidator.IsValidPort(port).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void ProxyPort_InvalidPorts(int port)
    {
        PrintPilotProxy.Core.Validation.NetworkValidator.IsValidPort(port).Should().BeFalse();
    }

    // ─── IP validation ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("192.168.1.100", true)]
    [InlineData("10.0.0.1",      true)]
    [InlineData("0.0.0.0",       true)]
    [InlineData("127.0.0.1",     true)]
    [InlineData("999.999.0.1",   false)]
    [InlineData("not-an-ip",     false)]
    [InlineData("",              false)]
    public void SpecificIp_Validation(string ip, bool expectedValid)
    {
        if (string.IsNullOrEmpty(ip))
        {
            PrintPilotProxy.Core.Validation.NetworkValidator.IsValidListenAddress(ip)
                .Should().BeFalse();
        }
        else
        {
            PrintPilotProxy.Core.Validation.NetworkValidator.IsValidListenAddress(ip)
                .Should().Be(expectedValid);
        }
    }
}
