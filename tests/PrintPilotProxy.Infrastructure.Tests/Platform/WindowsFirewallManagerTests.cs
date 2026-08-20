using System;
using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Platform;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Platform;

public class WindowsFirewallManagerTests
{
    [Theory]
    [InlineData(FirewallRuleNames.ManagedRule)]
    [InlineData(FirewallRuleNames.DiscoveryRule)]
    public void ValidateManagedRule_AcceptedRuleNames_DoesNotThrow(string ruleName)
    {
        var rule = new FirewallRule
        {
            Name = ruleName,
            Port = 3128,
            Protocol = "TCP",
            Direction = "Inbound",
            Action = "Allow",
            Profile = "Private"
        };

        var method = typeof(WindowsFirewallManager).GetMethod(
            "ValidateManagedRule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Action act = () =>
        {
            try
            {
                method?.Invoke(null, new object[] { rule });
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("UnauthorizedRule")]
    [InlineData("PrintPilotProxy; rm -rf")]
    [InlineData("")]
    public void ValidateManagedRule_UnauthorizedOrInvalidNames_ThrowsException(string ruleName)
    {
        var rule = new FirewallRule
        {
            Name = ruleName,
            Port = 3128,
            Protocol = "TCP"
        };

        var method = typeof(WindowsFirewallManager).GetMethod(
            "ValidateManagedRule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Action act = () =>
        {
            try
            {
                method?.Invoke(null, new object[] { rule });
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PrintPilotProxy may manage only its own firewall rules.");
    }
}
