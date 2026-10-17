using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;
using Xunit;
namespace PrintPilotProxy.Core.Tests.Validation;

/// <summary>PROXY-003 and PROXY-016.</summary>
public class ConfigurationRestartPolicyTests
{
    [Fact]
    public void IdenticalConfigurations_DoNotRequireRestart()
    {
        ConfigurationRestartPolicy.RequiresProxyRestart(new ProxyConfiguration(), new ProxyConfiguration()).Should().BeFalse();
    }

    [Fact]
    public void ChangingAuthentication_RequiresRestart()
    {
        var after = new ProxyConfiguration();
        after.Security.RequireAuthentication = true;
        ConfigurationRestartPolicy.RequiresProxyRestart(new ProxyConfiguration(), after).Should().BeTrue();
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("host")]
    [InlineData("port")]
    [InlineData("user")]
    [InlineData("password")]
    public void ChangingUpstreamProxy_RequiresRestart(string field)
    {
        var after = new ProxyConfiguration();
        switch (field)
        {
            case "mode": after.UpstreamProxy.Mode = UpstreamProxyMode.Manual; break;
            case "host": after.UpstreamProxy.Host = "proxy.example"; break;
            case "port": after.UpstreamProxy.Port = 9090; break;
            case "user": after.UpstreamProxy.Username = "svc"; break;
            case "password": after.UpstreamProxy.ProtectedPassword = "secret"; break;
        }

        ConfigurationRestartPolicy.RequiresProxyRestart(new ProxyConfiguration(), after).Should().BeTrue();
    }

    [Fact]
    public void ChangingOnlyLanguage_DoesNotRequireRestart()
    {
        var after = new ProxyConfiguration();
        after.Language.CultureName = "tr-TR";
        ConfigurationRestartPolicy.RequiresProxyRestart(new ProxyConfiguration(), after).Should().BeFalse();
    }

    [Fact]
    public void Validate_ManualUpstreamWithoutHost_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.UpstreamProxy.Mode = UpstreamProxyMode.Manual;
        config.UpstreamProxy.Host = null;
        ConfigurationValidator.Validate(config).Should().Contain(e => e.Contains("upstream proxy host"));
    }

    [Fact]
    public void Validate_UndefinedListenerMode_ReturnsError()
    {
        var config = new ProxyConfiguration();
        config.Listener.Mode = (ListenerMode)99;
        ConfigurationValidator.Validate(config).Should().Contain(e => e.Contains("Listener mode"));
    }

    [Fact]
    public void Validate_NullSection_ReturnsErrorInsteadOfThrowing()
    {
        var config = new ProxyConfiguration { Security = null! };
        ConfigurationValidator.Validate(config).Should().Contain(e => e.Contains("security section"));
    }
}
