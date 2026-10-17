using System.Net;
using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;
using Xunit;
namespace PrintPilotProxy.Core.Tests.Validation;

/// <summary>PROXY-002 (exposure warning), PROXY-004 (masked secret), PROXY-011 (logging limits).</summary>
public class AuditDecisionPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("203.0.113.5", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.168.1.10", false)]
    [InlineData("100.64.0.5", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::1", false)]
    public void IsPubliclyRoutable_ClassifiesAddresses(string address, bool expected)
    {
        NetworkAddressClassifier.IsPubliclyRoutable(IPAddress.Parse(address)).Should().Be(expected);
    }

    [Fact]
    public void Mask_HidesAStoredSecret_AndLeavesEmptyAlone()
    {
        ConfigurationSecrets.Mask("CIPHERTEXT").Should().Be(ConfigurationSecrets.UnchangedSentinel);
        ConfigurationSecrets.Mask(null).Should().BeNull();
        ConfigurationSecrets.Mask("").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_KeepsTheStoredSecretWhenThePlaceholderComesBack()
    {
        ConfigurationSecrets.Resolve(ConfigurationSecrets.UnchangedSentinel, "STORED").Should().Be("STORED");
        ConfigurationSecrets.Resolve("NEW", "STORED").Should().Be("NEW");
        ConfigurationSecrets.Resolve(null, "STORED").Should().BeNull();
    }

    [Fact]
    public void LoggingPolicy_UsesTheConfiguredLimits()
    {
        var settings = new LoggingSettings { RetentionDays = 10, MaxSizeMb = 100, MinimumLevel = "warning" };

        LoggingPolicy.RetainedFileCount(settings).Should().Be(10);
        LoggingPolicy.FileSizeLimitBytes(settings).Should().Be(10L * 1024 * 1024);
        LoggingPolicy.NormalizeMinimumLevel(settings).Should().Be("Warning");
    }

    [Fact]
    public void LoggingPolicy_FallsBackForMissingOrInvalidValues()
    {
        LoggingPolicy.RetainedFileCount(null).Should().Be(30);
        LoggingPolicy.RetainedFileCount(new LoggingSettings { RetentionDays = 0 }).Should().Be(30);
        LoggingPolicy.RetainedFileCount(new LoggingSettings { RetentionDays = 5000 }).Should().Be(365);
        LoggingPolicy.FileSizeLimitBytes(new LoggingSettings { MaxSizeMb = 1, RetentionDays = 30 }).Should().Be(1024L * 1024);
        LoggingPolicy.NormalizeMinimumLevel(new LoggingSettings { MinimumLevel = "loud" }).Should().Be("Information");
        LoggingPolicy.NormalizeMinimumLevel(null).Should().Be("Information");
    }
}
