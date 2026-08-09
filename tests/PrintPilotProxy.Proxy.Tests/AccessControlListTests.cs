using System.Net;
using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Proxy;

using Xunit;
namespace PrintPilotProxy.Proxy.Tests;

public class AccessControlListTests
{
    [Fact]
    public void IsAllowed_AllowedIp_ReturnsTrue()
    {
        var config = new ProxyConfiguration();
        config.AllowedClients.Add(new AllowedClient { Name = "Test", IpOrCidr = "192.168.1.100", Enabled = true });
        var acl = new AccessControlList(config);

        var result = acl.IsAllowed(IPAddress.Parse("192.168.1.100"));
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_DisallowedIp_ReturnsFalse()
    {
        var config = new ProxyConfiguration();
        config.AllowedClients.Add(new AllowedClient { Name = "Test", IpOrCidr = "192.168.1.100", Enabled = true });
        var acl = new AccessControlList(config);

        var result = acl.IsAllowed(IPAddress.Parse("192.168.1.101"));
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_CidrRange_ReturnsTrue()
    {
        var config = new ProxyConfiguration();
        config.AllowedClients.Add(new AllowedClient { Name = "Test", IpOrCidr = "10.0.0.0/24", Enabled = true });
        var acl = new AccessControlList(config);

        var result = acl.IsAllowed(IPAddress.Parse("10.0.0.50"));
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_DisabledClient_ReturnsFalse()
    {
        var config = new ProxyConfiguration();
        config.AllowedClients.Add(new AllowedClient { Name = "Test", IpOrCidr = "192.168.1.100", Enabled = false });
        var acl = new AccessControlList(config);

        var result = acl.IsAllowed(IPAddress.Parse("192.168.1.100"));
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_EmptyAcl_ReturnsFalse()
    {
        var config = new ProxyConfiguration();
        var acl = new AccessControlList(config);

        var result = acl.IsAllowed(IPAddress.Parse("192.168.1.100"));
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDestinationPortAllowed_AllowedPort_ReturnsTrue()
    {
        var config = new ProxyConfiguration();
        config.Security.DestinationPortRestrictionsEnabled = true;
        config.Security.AllowedDestinationPorts = new List<int> { 80, 443 };
        var acl = new AccessControlList(config);

        var result = acl.IsDestinationPortAllowed(443);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDestinationPortAllowed_DisallowedPort_ReturnsFalse()
    {
        var config = new ProxyConfiguration();
        config.Security.DestinationPortRestrictionsEnabled = true;
        config.Security.AllowedDestinationPorts = new List<int> { 80, 443 };
        var acl = new AccessControlList(config);

        var result = acl.IsDestinationPortAllowed(8080);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDestinationPortAllowed_RestrictionsDisabled_ReturnsTrue()
    {
        var config = new ProxyConfiguration();
        config.Security.DestinationPortRestrictionsEnabled = false;
        var acl = new AccessControlList(config);

        var result = acl.IsDestinationPortAllowed(8080);
        result.Should().BeTrue();
    }

    [Fact]
    public void Refresh_UpdatesRules()
    {
        var config = new ProxyConfiguration();
        var acl = new AccessControlList(config);
        
        acl.IsAllowed(IPAddress.Parse("192.168.1.100")).Should().BeFalse();

        config.AllowedClients.Add(new AllowedClient { Name = "Test", IpOrCidr = "192.168.1.100", Enabled = true });
        acl.Refresh(config);

        acl.IsAllowed(IPAddress.Parse("192.168.1.100")).Should().BeTrue();
    }
}
