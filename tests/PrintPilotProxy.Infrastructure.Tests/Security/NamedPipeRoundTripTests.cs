using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Ipc;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Security;

/// <summary>Real named-pipe tests: the local path still works after PROXY-007/008, and the pipe refuses network callers.</summary>
public class NamedPipeRoundTripTests
{
    private static bool PipeAlreadyExists() => File.Exists(@"\\.\pipe\" + NamedPipeIpcServer.PipeName);

    // A non-elevated test process cannot create a second server instance (the pipe ACL reserves CreateNewInstance
    // for LocalSystem/Administrators), so each test below uses exactly one connection.
    [Fact]
    public async Task LocalClient_CanExchangeMessages()
    {
        if (!OperatingSystem.IsWindows() || PipeAlreadyExists())
        {
            return; // Another instance owns the fixed pipe name on this machine; do not interfere with it.
        }

        await using var server = new NamedPipeIpcServer();
        server.MessageReceived += message => Task.FromResult(new IpcMessage
        {
            Type = IpcMessageTypes.Success,
            CorrelationId = message.CorrelationId,
            Payload = "pong"
        });
        await server.StartAsync();

        try
        {
            using var client = new NamedPipeIpcClient();
            var reply = await client.SendAsync(new IpcMessage { Type = IpcMessageTypes.GetStatus });
            reply.Type.Should().Be(IpcMessageTypes.Success);
            reply.Payload.Should().Be("pong");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task OversizedMessage_IsAnsweredWithAnErrorAndRejected()
    {
        if (!OperatingSystem.IsWindows() || PipeAlreadyExists())
        {
            return;
        }

        await using var server = new NamedPipeIpcServer();
        await server.StartAsync();
        try
        {
            // 1 MiB + 10 characters on one line must be answered with an error and the connection closed.
            using var raw = new NamedPipeClientStream(".", NamedPipeIpcServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await raw.ConnectAsync(5000, cts.Token);
            var big = Encoding.UTF8.GetBytes(new string('x', 1024 * 1024 + 10) + "\n");
            await raw.WriteAsync(big, cts.Token);
            using var reader = new StreamReader(raw, Encoding.UTF8, false, 4096, leaveOpen: true);
            var line = await reader.ReadLineAsync(cts.Token);
            line.Should().Contain("exceeds the permitted size");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task PipeAcl_DeniesNetworkCallers()
    {
        if (!OperatingSystem.IsWindows() || PipeAlreadyExists())
        {
            return;
        }

        await using var server = new NamedPipeIpcServer();
        await server.StartAsync();
        try
        {
            using var raw = new NamedPipeClientStream(".", NamedPipeIpcServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await raw.ConnectAsync(5000, cts.Token);

            var acl = raw.GetAccessControl();
            var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier));
            var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
            var denied = false;
            foreach (PipeAccessRule rule in rules)
            {
                if (rule.IdentityReference.Equals(networkSid) && rule.AccessControlType == AccessControlType.Deny)
                {
                    denied = true;
                }
            }

            denied.Should().BeTrue();
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
