using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Discovery;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Discovery;

public class UdpBroadcastDiscoveryTransportTests
{
    [Fact]
    public async Task Transport_EndToEnd_ReceivesRequestAndSendsResponse()
    {
        // Use a dynamic test port to avoid collision in concurrent test execution
        var testPort = 37425;
        var rateLimiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 20);
        var transport = new UdpBroadcastDiscoveryTransport(rateLimiter, NullLogger<UdpBroadcastDiscoveryTransport>.Instance, testPort);

        try
        {
            await transport.StartAsync((req, clientEp) =>
            {
                var resp = new DiscoveryResponse
                {
                    Service = "PrintPilotProxy",
                    ProtocolVersion = 1,
                    Version = "0.5.0",
                    Host = "127.0.0.1",
                    ProxyPort = 3128,
                    InstanceId = "test-guid-1",
                    Protocol = "http-connect"
                };
                return Task.FromResult<DiscoveryResponse?>(resp);
            });

            transport.IsRunning.Should().BeTrue();

            // Act: send UDP request from a test client
            using var testClient = new UdpClient();
            var request = new DiscoveryRequest
            {
                Service = "PrintPilotProxy",
                ProtocolVersion = 1,
                Request = "discover"
            };
            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);
            await testClient.SendAsync(requestBytes, requestBytes.Length, new IPEndPoint(IPAddress.Loopback, testPort));

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var receiveResult = await testClient.ReceiveAsync(cts.Token);

            receiveResult.Buffer.Should().NotBeEmpty();
            var response = JsonSerializer.Deserialize<DiscoveryResponse>(receiveResult.Buffer);
            response.Should().NotBeNull();
            response!.Service.Should().Be("PrintPilotProxy");
            response.Host.Should().Be("127.0.0.1");
            response.ProxyPort.Should().Be(3128);
            response.InstanceId.Should().Be("test-guid-1");
        }
        finally
        {
            await transport.StopAsync();
            transport.IsRunning.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Transport_MalformedPacket_DoesNotCrash()
    {
        var testPort = 37426;
        var rateLimiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 20);
        var transport = new UdpBroadcastDiscoveryTransport(rateLimiter, NullLogger<UdpBroadcastDiscoveryTransport>.Instance, testPort);

        try
        {
            var handlerCalled = false;
            await transport.StartAsync((req, clientEp) =>
            {
                handlerCalled = true;
                return Task.FromResult<DiscoveryResponse?>(null);
            });

            using var testClient = new UdpClient();
            var garbageBytes = Encoding.UTF8.GetBytes("THIS_IS_NOT_VALID_JSON_GARBAGE_BYTES_12345");
            await testClient.SendAsync(garbageBytes, garbageBytes.Length, new IPEndPoint(IPAddress.Loopback, testPort));

            // Give receive loop a moment to process
            await Task.Delay(200);

            transport.IsRunning.Should().BeTrue();
            handlerCalled.Should().BeFalse();
        }
        finally
        {
            await transport.StopAsync();
        }
    }
}
