using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using Xunit;

namespace PrintPilotProxy.App.Tests;

/// <summary>
/// Tests for IpcClientService — verifies message routing and response parsing.
/// Uses a mock IIpcClient; no real named pipe is created.
/// </summary>
public class IpcClientServiceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static IpcClientService BuildService(Mock<IIpcClient> mock)
        => new(mock.Object);

    private static Mock<IIpcClient> ConnectedMock()
    {
        var m = new Mock<IIpcClient>();
        m.Setup(c => c.IsConnected).Returns(true);
        m.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return m;
    }

    // ─── GetStatusAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ReturnsStatus_WhenServiceResponds()
    {
        var expected = new ProxyStatus
        {
            State           = ProxyState.Running,
            ListeningAddress = "127.0.0.1:3128",
            TotalRequests   = 42
        };
        var mock = ConnectedMock();
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.GetStatus),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.StatusResponse,
                Payload = JsonSerializer.Serialize(expected, JsonOpts)
            });

        var svc = BuildService(mock);
        var result = await svc.GetStatusAsync();

        result.Should().NotBeNull();
        result!.State.Should().Be(ProxyState.Running);
        result.ListeningAddress.Should().Be("127.0.0.1:3128");
        result.TotalRequests.Should().Be(42);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenServiceUnreachable()
    {
        var mock = new Mock<IIpcClient>();
        mock.Setup(c => c.IsConnected).Returns(false);
        mock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        mock.Setup(c => c.SendAsync(It.IsAny<IpcMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.IO.IOException("Pipe not found"));

        var svc = BuildService(mock);
        var result = await svc.GetStatusAsync();

        result.Should().BeNull();
    }

    // ─── StartProxyAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartProxyAsync_ReturnsSuccess_WhenServiceAcks()
    {
        var mock = ConnectedMock();
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.StartProxy),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage { Type = IpcMessageTypes.Success, Payload = "Proxy started." });

        var svc = BuildService(mock);
        var (success, message) = await svc.StartProxyAsync();

        success.Should().BeTrue();
        message.Should().Be("Proxy started.");
    }

    [Fact]
    public async Task StartProxyAsync_ReturnsFailure_WhenServiceReturnsError()
    {
        var mock = ConnectedMock();
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.StartProxy),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage { Type = IpcMessageTypes.Error, Payload = "Already running." });

        var svc = BuildService(mock);
        var (success, message) = await svc.StartProxyAsync();

        success.Should().BeFalse();
        message.Should().Be("Already running.");
    }

    // ─── UpdateConfigurationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateConfigurationAsync_SendsConfigPayload()
    {
        var mock = ConnectedMock();
        IpcMessage? captured = null;
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.UpdateConfiguration),
            It.IsAny<CancellationToken>()))
            .Callback<IpcMessage, CancellationToken>((msg, _) => captured = msg)
            .ReturnsAsync(new IpcMessage { Type = IpcMessageTypes.Success, Payload = "Applied." });

        var svc = BuildService(mock);
        var cfg = new ProxyConfiguration { Listener = new ListenerSettings { Port = 9999 } };
        var (success, _) = await svc.UpdateConfigurationAsync(cfg);

        success.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Payload.Should().Contain("9999");
    }

    // ─── GetConfigurationAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetConfigurationAsync_DeserializesConfig()
    {
        var cfg = new ProxyConfiguration
        {
            Listener = new ListenerSettings { Port = 8080, Mode = ListenerMode.Auto }
        };
        var mock = ConnectedMock();
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.GetConfiguration),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.ConfigurationResponse,
                Payload = JsonSerializer.Serialize(cfg, JsonOpts)
            });

        var svc = BuildService(mock);
        var result = await svc.GetConfigurationAsync();

        result.Should().NotBeNull();
        result!.Listener.Port.Should().Be(8080);
        result.Listener.Mode.Should().Be(ListenerMode.Auto);
    }

    // ─── GetRecentRequestsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetRecentRequestsAsync_ReturnsEmptyList_OnNullPayload()
    {
        var mock = ConnectedMock();
        mock.Setup(c => c.SendAsync(
            It.Is<IpcMessage>(m => m.Type == IpcMessageTypes.GetRecentRequests),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IpcMessage
            {
                Type    = IpcMessageTypes.RecentRequestsResponse,
                Payload = null
            });

        var svc = BuildService(mock);
        var result = await svc.GetRecentRequestsAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
