using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;
using Xunit;
namespace PrintPilotProxy.Core.Tests.Validation;

public class ConfigurationValidatorTests
{
    [Fact]
    public void Validate_DefaultConfig_ReturnsValid()
    {
        var config = new ProxyConfiguration();
        var result = ConfigurationValidator.Validate(config);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidListenAddress_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.Listener.Mode = ListenerMode.SpecificAddress;
        config.Listener.ListenAddress = "invalid";
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("Listen address"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_InvalidPort_ReturnsError(int port)
    {
        var config = new ProxyConfiguration();
        config.Listener.Port = port;
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("Port"));
    }

    [Fact]
    public void Validate_EmptyClientName_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.ClientAccess.AllowedClients.Add(new AllowedClient { Name = "", IpOrCidr = "192.168.1.1" });
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void Validate_DuplicateClientNames_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.ClientAccess.AllowedClients.Add(new AllowedClient { Name = "Client1", IpOrCidr = "192.168.1.1" });
        config.ClientAccess.AllowedClients.Add(new AllowedClient { Name = "Client1", IpOrCidr = "192.168.1.2" });
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_InvalidClientIp_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.ClientAccess.AllowedClients.Add(new AllowedClient { Name = "Client1", IpOrCidr = "invalid" });
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("invalid IP/CIDR"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_InvalidDestinationPort_ReturnsError(int port)
    {
        var config = new ProxyConfiguration();
        config.Security.AllowedDestinationPorts.Add(port);
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("destination port"));
    }

    [Fact]
    public void Validate_OutOfRangeRetention_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.Logging.RetentionDays = -1;
        var result = ConfigurationValidator.Validate(config);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Contains("retention"));
    }

    [Fact]
    public void GetWarnings_NoClients_ReturnsWarning()
    {
        var config = new ProxyConfiguration();
        config.ClientAccess.Mode = ClientAccessMode.AllowList;
        var warnings = ConfigurationValidator.GetWarnings(config);
        warnings.Should().Contain(w => w.Contains("No allowed clients"));
    }

    [Fact]
    public void GetWarnings_AllInterfaces_ReturnsWarning()
    {
        var config = new ProxyConfiguration();
        config.Listener.Mode = ListenerMode.SpecificAddress;
        config.Listener.ListenAddress = "0.0.0.0";
        var warnings = ConfigurationValidator.GetWarnings(config);
        warnings.Should().Contain(w => w.Contains("all interfaces"));
    }

    [Fact]
    public void GetWarnings_BroadSubnet_ReturnsWarning()
    {
        var config = new ProxyConfiguration();
        config.ClientAccess.AllowedClients.Add(new AllowedClient { Name = "All", IpOrCidr = "0.0.0.0/0" });
        var warnings = ConfigurationValidator.GetWarnings(config);
        warnings.Should().Contain(w => w.Contains("open proxy", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetWarnings_DisabledPortRestrictions_ReturnsWarning()
    {
        var config = new ProxyConfiguration();
        config.Security.DestinationPortRestrictionsEnabled = false;
        var warnings = ConfigurationValidator.GetWarnings(config);
        warnings.Should().Contain(w => w.Contains("port restrictions"));
    }
}
