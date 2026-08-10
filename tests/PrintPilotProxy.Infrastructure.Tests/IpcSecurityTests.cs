using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Ipc;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests;

/// <summary>
/// Comprehensive automated security tests for the IPC authorization architecture.
/// Verifies that authorized desktop/system users can perform allowed operations, while
/// unauthorized local users are denied privileged commands (configuration changes, service control, firewall).
/// </summary>
public class IpcSecurityTests
{
    private readonly IpcSecurityValidator _validator = new(NullLogger<IpcSecurityValidator>.Instance);

    [Theory]
    [InlineData(IpcMessageTypes.GetStatus)]
    [InlineData(IpcMessageTypes.GetConfiguration)]
    [InlineData(IpcMessageTypes.GetRecentRequests)]
    [InlineData(IpcMessageTypes.GetNetworkInterfaces)]
    [InlineData(IpcMessageTypes.GetFirewallStatus)]
    [InlineData(IpcMessageTypes.RunDiagnostics)]
    [InlineData(IpcMessageTypes.GetSecurityAudit)]
    public void ReadOnlyCommands_AreClassifiedCorrectly(string commandType)
    {
        _validator.IsReadOnlyCommand(commandType).Should().BeTrue();
        _validator.IsPrivilegedCommand(commandType).Should().BeFalse();
    }

    [Theory]
    [InlineData(IpcMessageTypes.StartProxy)]
    [InlineData(IpcMessageTypes.StopProxy)]
    [InlineData(IpcMessageTypes.RestartProxy)]
    [InlineData(IpcMessageTypes.UpdateConfiguration)]
    [InlineData(IpcMessageTypes.ApplyFirewallRule)]
    [InlineData(IpcMessageTypes.RemoveFirewallRule)]
    public void PrivilegedCommands_AreClassifiedCorrectly(string commandType)
    {
        _validator.IsPrivilegedCommand(commandType).Should().BeTrue();
        _validator.IsReadOnlyCommand(commandType).Should().BeFalse();
    }

    [Fact]
    public void Requirement1_To_4_AuthorizedDesktopUser_CanReadStatusConfig_And_UpdateConfiguration()
    {
        var authorizedIdentity = IpcClientIdentity.CreateInteractiveUser();

        // 1. Authorized desktop user can read status
        var statusAuthorized = _validator.IsAuthorized(authorizedIdentity, IpcMessageTypes.GetStatus, out var statusError);
        statusAuthorized.Should().BeTrue();
        statusError.Should().BeNull();

        // 2. Authorized desktop user can read configuration
        var configReadAuthorized = _validator.IsAuthorized(authorizedIdentity, IpcMessageTypes.GetConfiguration, out var configReadError);
        configReadAuthorized.Should().BeTrue();
        configReadError.Should().BeNull();

        // 3. Authorized desktop user can update configuration
        var configUpdateAuthorized = _validator.IsAuthorized(authorizedIdentity, IpcMessageTypes.UpdateConfiguration, out var configUpdateError);
        configUpdateAuthorized.Should().BeTrue();
        configUpdateError.Should().BeNull();

        // 4. Authorized desktop user can start/stop service
        var startProxyAuthorized = _validator.IsAuthorized(authorizedIdentity, IpcMessageTypes.StartProxy, out var startProxyError);
        startProxyAuthorized.Should().BeTrue();
        startProxyError.Should().BeNull();
    }

    [Theory]
    [InlineData(IpcMessageTypes.UpdateConfiguration)]
    [InlineData(IpcMessageTypes.StartProxy)]
    [InlineData(IpcMessageTypes.StopProxy)]
    [InlineData(IpcMessageTypes.RestartProxy)]
    [InlineData(IpcMessageTypes.ApplyFirewallRule)]
    [InlineData(IpcMessageTypes.RemoveFirewallRule)]
    public void Requirement5_To_8_UnauthorizedLocalUser_CannotInvokePrivilegedCommands(string privilegedCommand)
    {
        var unauthorizedIdentity = IpcClientIdentity.CreateUnauthorizedUser();

        var authorized = _validator.IsAuthorized(unauthorizedIdentity, privilegedCommand, out var failureReason);

        authorized.Should().BeFalse($"Unauthorized user (User B) must NOT be permitted to execute privileged IPC command '{privilegedCommand}'");
        failureReason.Should().Contain("Access denied");
    }

    [Fact]
    public void Requirement5_UnauthorizedLocalUser_CannotChangeProxyConfiguration()
    {
        var unauthorizedIdentity = IpcClientIdentity.CreateUnauthorizedUser();

        var authorized = _validator.IsAuthorized(unauthorizedIdentity, IpcMessageTypes.UpdateConfiguration, out var failureReason);

        authorized.Should().BeFalse();
        failureReason.Should().Contain("Access denied");
        failureReason.Should().Contain("UpdateConfiguration");
    }

    [Fact]
    public void Requirement7_UnauthorizedLocalUser_CannotControlService()
    {
        var unauthorizedIdentity = IpcClientIdentity.CreateUnauthorizedUser();

        foreach (var serviceControlCommand in new[] { IpcMessageTypes.StartProxy, IpcMessageTypes.StopProxy, IpcMessageTypes.RestartProxy })
        {
            var authorized = _validator.IsAuthorized(unauthorizedIdentity, serviceControlCommand, out var failureReason);
            authorized.Should().BeFalse($"Unauthorized user must not execute service control command '{serviceControlCommand}'");
            failureReason.Should().Contain("Access denied");
        }
    }

    [Fact]
    public void Requirement8_UnauthorizedLocalUser_CannotModifyFirewallConfiguration()
    {
        var unauthorizedIdentity = IpcClientIdentity.CreateUnauthorizedUser();

        foreach (var firewallCommand in new[] { IpcMessageTypes.ApplyFirewallRule, IpcMessageTypes.RemoveFirewallRule })
        {
            var authorized = _validator.IsAuthorized(unauthorizedIdentity, firewallCommand, out var failureReason);
            authorized.Should().BeFalse($"Unauthorized user must not execute firewall modification command '{firewallCommand}'");
            failureReason.Should().Contain("Access denied");
        }
    }
}
