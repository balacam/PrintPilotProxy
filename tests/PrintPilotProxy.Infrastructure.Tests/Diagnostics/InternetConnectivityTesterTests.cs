using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Diagnostics;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Diagnostics;

public class InternetConnectivityTesterTests : IDisposable
{
    private readonly Mock<IDataProtector> _mockDataProtector;
    private readonly InternetConnectivityTester _tester;
    private TcpListener _listener;
    private Task _listenerTask;
    private CancellationTokenSource _cts;

    public InternetConnectivityTesterTests()
    {
        _mockDataProtector = new Mock<IDataProtector>();
        _tester = new InternetConnectivityTester(_mockDataProtector.Object);
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Stop();
        _cts.Dispose();
    }

    private int StartMockServer(Func<StreamReader, StreamWriter, Task> handler)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _listenerTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        using (var stream = client.GetStream())
                        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
                        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true })
                        {
                            await handler(reader, writer);
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        });

        return port;
    }

    [Fact]
    public async Task RunTestAsync_DirectMode_Success_ReturnsConnectedDirect()
    {
        var config = new ProxyConfiguration();
        config.UpstreamProxy.Mode = UpstreamProxyMode.Direct;

        var port = StartMockServer(async (reader, writer) =>
        {
            await Task.Delay(10); // keep alive briefly
        });

        _tester.TargetHost = "127.0.0.1";
        _tester.TargetPort = port;

        var result = await _tester.RunTestAsync(config, _cts.Token);
        result.Should().Be("ConnectedDirect");
    }

    [Fact]
    public async Task RunTestAsync_UpstreamMode_Success_ReturnsConnectedViaUpstream()
    {
        var config = new ProxyConfiguration();
        config.UpstreamProxy.Mode = UpstreamProxyMode.Manual;
        config.UpstreamProxy.Host = "127.0.0.1";
        config.UpstreamProxy.Username = "user";
        config.UpstreamProxy.ProtectedPassword = "protected_password";

        _mockDataProtector.Setup(d => d.Unprotect("protected_password")).Returns("pass");

        var port = StartMockServer(async (reader, writer) =>
        {
            string line;
            bool hasAuth = false;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                if (line.Contains("Proxy-Authorization")) hasAuth = true;
            }
            
            if (hasAuth)
            {
                await writer.WriteLineAsync("HTTP/1.1 200 Connection established\r\n\r\n");
            }
            else
            {
                await writer.WriteLineAsync("HTTP/1.1 500 Error\r\n\r\n");
            }
            await Task.Delay(500); // keep alive so client can read
        });

        config.UpstreamProxy.Port = port;
        _tester.TargetHost = "127.0.0.1";
        _tester.TargetPort = port + 1; // arbitrary target

        var result = await _tester.RunTestAsync(config, _cts.Token);
        result.Should().Be("ConnectedViaUpstream");
    }

    [Fact]
    public async Task RunTestAsync_UpstreamMode_AuthFailed_ReturnsAuthenticationFailed()
    {
        var config = new ProxyConfiguration();
        config.UpstreamProxy.Mode = UpstreamProxyMode.Manual;
        config.UpstreamProxy.Host = "127.0.0.1";
        
        var port = StartMockServer(async (reader, writer) =>
        {
            var line1 = await reader.ReadLineAsync();
            await writer.WriteLineAsync("HTTP/1.1 407 Proxy Authentication Required\r\n\r\n");
            await Task.Delay(500); // keep alive
        });

        config.UpstreamProxy.Port = port;
        _tester.TargetHost = "127.0.0.1";
        _tester.TargetPort = port + 1;

        var result = await _tester.RunTestAsync(config, _cts.Token);
        result.Should().Be("UpstreamAuthenticationFailed");
    }
}
