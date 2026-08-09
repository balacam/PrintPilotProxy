using FluentAssertions;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Security;
using Xunit;
namespace PrintPilotProxy.Core.Tests.Security;

public class SecurityAuditorTests
{
    [Fact]
    public void Audit_DefaultConfig_ReturnsExpectedResults()
    {
        var config = new ProxyConfiguration();
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        result.Checks.Should().NotBeNull();
    }

    [Fact]
    public void Audit_ListenAllInterfaces_WarnsAboutPublicAccess()
    {
        var config = new ProxyConfiguration();
        config.Listener.ListenAddress = "0.0.0.0";
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        result.Checks.Should().Contain(c => c.Level == SecurityLevel.Warning && c.Message.Contains("listening on all interfaces"));
    }

    [Fact]
    public void Audit_NoClients_ShowsInfo()
    {
        var config = new ProxyConfiguration();
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        result.Checks.Should().Contain(c => c.Level == SecurityLevel.Info && c.Message.Contains("No allowed clients"));
    }

    [Fact]
    public void Audit_BroadSubnet_Warns()
    {
        var config = new ProxyConfiguration();
        config.AllowedClients.Add(new AllowedClient { Name = "All", IpOrCidr = "10.0.0.0/8" });
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        result.Checks.Should().Contain(c => c.Level == SecurityLevel.Warning && c.Message.Contains("broad subnet"));
    }

    [Fact]
    public void Audit_DisabledPortRestrictions_Warns()
    {
        var config = new ProxyConfiguration();
        config.Security.DestinationPortRestrictionsEnabled = false;
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        result.Checks.Should().Contain(c => c.Level == SecurityLevel.Warning && c.Message.Contains("port restrictions"));
    }

    [Fact]
    public void Audit_HttpsInterception_AlwaysPassesAsDisabled()
    {
        var config = new ProxyConfiguration();
        var auditor = new SecurityAuditor();
        var result = auditor.Audit(config);
        
        // Ensure there is no warning about HTTPS interception since it is inherently disabled
        result.Checks.Should().Contain(c => c.Name == "HTTPS interception disabled" && c.Passed);
    }
}
