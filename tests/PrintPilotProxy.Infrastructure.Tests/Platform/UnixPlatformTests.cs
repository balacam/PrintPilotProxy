using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Ipc;
using PrintPilotProxy.Infrastructure.Platform;
using PrintPilotProxy.Infrastructure.Security;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Platform;

/// <summary>Linux/macOS implementations. Tests that touch Unix permissions or sockets skip themselves on Windows.</summary>
public sealed class UnixPlatformTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ppp_unix_" + Guid.NewGuid().ToString("N"));

    public UnixPlatformTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void KeyFileProtector_RoundTripsAndNeverStoresPlaintext()
    {
        var protector = new KeyFileDataProtector(Path.Combine(_root, "secret.key"));

        var sealedValue = protector.Protect("s3cret-password");

        sealedValue.Should().StartWith("v1:").And.NotContain("s3cret");
        protector.Unprotect(sealedValue).Should().Be("s3cret-password");
        protector.Protect("s3cret-password").Should().NotBe(sealedValue, "every value gets a fresh nonce");
    }

    [Fact]
    public void KeyFileProtector_HandlesEmptyAndUnreadableInputWithoutThrowing()
    {
        var protector = new KeyFileDataProtector(Path.Combine(_root, "secret.key"));

        protector.Protect("").Should().BeEmpty();
        protector.Unprotect("").Should().BeEmpty();
        protector.Unprotect("not-base64-and-no-prefix").Should().BeEmpty("a Windows DPAPI blob is not readable here");
        protector.Unprotect("v1:!!!").Should().BeEmpty();
        protector.Unprotect("v1:" + Convert.ToBase64String(new byte[40])).Should().BeEmpty("a value without a key file is unreadable");
    }

    [Fact]
    public void KeyFileProtector_RejectsTamperedAndForeignKeyValues()
    {
        var protector = new KeyFileDataProtector(Path.Combine(_root, "a.key"));
        var sealedValue = protector.Protect("secret");

        var bytes = Convert.FromBase64String(sealedValue[3..]);
        bytes[^1] ^= 0xFF;
        protector.Unprotect("v1:" + Convert.ToBase64String(bytes)).Should().BeEmpty("GCM authentication fails");

        new KeyFileDataProtector(Path.Combine(_root, "b.key")).Unprotect(sealedValue).Should().BeEmpty("another key cannot read it");
    }

    [Fact]
    public void KeyFileProtector_ReusesTheKeyAcrossInstancesAndKeepsItPrivate()
    {
        var path = Path.Combine(_root, "secret.key");
        var sealedValue = new KeyFileDataProtector(path).Protect("persisted");

        new KeyFileDataProtector(path).Unprotect(sealedValue).Should().Be("persisted");

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void PrivateFileAcl_OnUnixIsOwnerOnly()
    {
        if (OperatingSystem.IsWindows()) return;

        var file = Path.Combine(_root, "config.json");
        File.WriteAllText(file, "{}");
        var folder = Path.Combine(_root, "backups");
        Directory.CreateDirectory(folder);

        PrivateFileAcl.Apply(file).Should().BeTrue();
        PrivateFileAcl.Apply(folder).Should().BeTrue();

        File.GetUnixFileMode(file).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(folder).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        PrivateFileAcl.Apply(Path.Combine(_root, "missing.json")).Should().BeFalse();
    }

    [Fact]
    public void UnixPathProvider_HonoursTheRootOverrideAndLocksDownSecrets()
    {
        if (OperatingSystem.IsWindows()) return;

        var previous = Environment.GetEnvironmentVariable(UnixPathProvider.RootOverrideVariable);
        Environment.SetEnvironmentVariable(UnixPathProvider.RootOverrideVariable, _root);
        try
        {
            var provider = new UnixPathProvider();
            provider.ConfigurationFilePath.Should().Be(Path.Combine(_root, "etc", "config.json"));

            provider.EnsureDirectoriesExist();

            Directory.Exists(provider.LogDirectory).Should().BeTrue();
            Directory.Exists(provider.BackupDirectory).Should().BeTrue();
            File.GetUnixFileMode(provider.DataDirectory).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.GetUnixFileMode(provider.BackupDirectory).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UnixPathProvider.RootOverrideVariable, previous);
        }
    }

    [Theory]
    [InlineData("active", ServiceStatus.Running)]
    [InlineData("activating", ServiceStatus.Starting)]
    [InlineData("deactivating", ServiceStatus.Stopping)]
    [InlineData("inactive", ServiceStatus.Stopped)]
    [InlineData("failed", ServiceStatus.Stopped)]
    [InlineData("weird", ServiceStatus.Unknown)]
    [InlineData(null, ServiceStatus.Unknown)]
    public void SystemdActiveState_MapsToServiceStatus(string? state, ServiceStatus expected)
        => LinuxServiceManager.ParseStatus(state).Should().Be(expected);

    [Theory]
    [InlineData("enabled", ServiceStartupType.Automatic)]
    [InlineData("disabled", ServiceStartupType.Disabled)]
    [InlineData("masked", ServiceStartupType.Disabled)]
    [InlineData("", ServiceStartupType.Unknown)]
    public void SystemdUnitFileState_MapsToStartupType(string state, ServiceStartupType expected)
        => LinuxServiceManager.ParseStartup(state).Should().Be(expected);

    [Fact]
    public void SystemctlShowOutput_IsParsedIntoProperties()
    {
        var properties = LinuxServiceManager.ParseProperties("LoadState=loaded\nActiveState=active\nUnitFileState=enabled\n");

        properties["LoadState"].Should().Be("loaded");
        properties["ActiveState"].Should().Be("active");
        properties["UnitFileState"].Should().Be("enabled");
    }

    [Fact]
    public async Task UnsupportedFirewall_ReportsNothingManagedInsteadOfPretending()
    {
        var firewall = new UnsupportedFirewallManager();

        firewall.HasPermission.Should().BeFalse();
        (await firewall.CreateRuleAsync(new FirewallRule { Port = 3128 })).Should().BeFalse();
        var status = await firewall.GetStatusAsync(FirewallRuleNames.ManagedRule);
        status.RuleExists.Should().BeFalse();
        status.ErrorMessage.Should().Contain("3128");
    }

    [Fact]
    public async Task UnixSocket_ExchangesMessagesAndIsNotWorldAccessible()
    {
        if (OperatingSystem.IsWindows()) return;

        var socket = Path.Combine(_root, "ipc.sock");
        var previous = Environment.GetEnvironmentVariable(IpcEndpoint.SocketOverrideVariable);
        Environment.SetEnvironmentVariable(IpcEndpoint.SocketOverrideVariable, socket);
        try
        {
            await using var server = new NamedPipeIpcServer();
            server.MessageReceived += message => Task.FromResult(new IpcMessage
            {
                Type = IpcMessageTypes.Success,
                CorrelationId = message.CorrelationId,
                Payload = "pong"
            });
            await server.StartAsync();

            using var client = new NamedPipeIpcClient();
            var reply = await client.SendAsync(new IpcMessage { Type = IpcMessageTypes.GetStatus });

            reply.Type.Should().Be(IpcMessageTypes.Success);
            reply.Payload.Should().Be("pong");
            (File.GetUnixFileMode(socket) & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute))
                .Should().Be(UnixFileMode.None, "only the owner and group may connect");

            await server.StopAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(IpcEndpoint.SocketOverrideVariable, previous);
        }
    }
}
