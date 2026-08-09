using System.Net;
using FluentAssertions;
using PrintPilotProxy.Core.Validation;
using Xunit;

namespace PrintPilotProxy.Core.Tests.Validation;

public class NetworkValidatorTests
{
    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("::1", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("256.0.0.1", false)]
    [InlineData("not_an_ip", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIpAddress_ShouldReturnExpectedResult(string? ip, bool expected)
    {
        var result = NetworkValidator.IsValidIpAddress(ip!);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.0/24", true)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("172.16.0.0/16", true)]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("192.168.1.1/32", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("192.168.1.0", false)]
    [InlineData("192.168.1.0/33", false)]
    [InlineData("192.168.1.0/-1", false)]
    [InlineData("not_a_cidr", false)]
    public void IsValidCidr_ShouldReturnExpectedResult(string cidr, bool expected)
    {
        var result = NetworkValidator.IsValidCidr(cidr);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("192.168.1.0/24", true)]
    [InlineData("invalid", false)]
    public void IsValidIpOrCidr_ShouldReturnExpectedResult(string input, bool expected)
    {
        var result = NetworkValidator.IsValidIpOrCidr(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.5", "192.168.1.0/24", true)]
    [InlineData("192.168.1.255", "192.168.1.0/24", true)]
    [InlineData("192.168.2.1", "192.168.1.0/24", false)]
    [InlineData("10.5.0.1", "10.0.0.0/8", true)]
    [InlineData("192.168.1.1", "0.0.0.0/0", true)]
    public void IsInCidrRange_ShouldReturnExpectedResult(string ip, string cidr, bool expected)
    {
        var result = NetworkValidator.IsInCidrRange(IPAddress.Parse(ip), cidr);
        result.Should().Be(expected);
    }

    [Fact]
    public void IsInCidrRange_DifferentAddressFamilies_ReturnsFalse()
    {
        var result = NetworkValidator.IsInCidrRange(IPAddress.Parse("::1"), "192.168.1.0/24");
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1", true)]
    [InlineData("192.168.1.1", "192.168.1.2", false)]
    [InlineData("192.168.1.5", "192.168.1.0/24", true)]
    [InlineData("192.168.2.1", "192.168.1.0/24", false)]
    public void IsMatch_ShouldReturnExpectedResult(string ip, string rule, bool expected)
    {
        var result = NetworkValidator.IsMatch(IPAddress.Parse(ip), rule);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("172.16.0.0/12", true)]
    [InlineData("192.168.0.0/16", true)]
    [InlineData("192.168.1.0/24", false)]
    [InlineData("192.168.1.1/32", false)]
    public void GetBroadSubnetWarning_ShouldReturnExpectedResult(string cidr, bool warns)
    {
        var result = NetworkValidator.GetBroadSubnetWarning(cidr);
        if (warns)
        {
            result.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(80, true)]
    [InlineData(443, true)]
    [InlineData(65535, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(65536, false)]
    public void IsValidPort_ShouldReturnExpectedResult(int port, bool expected)
    {
        var result = NetworkValidator.IsValidPort(port);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("invalid", false)]
    public void IsValidListenAddress_ShouldReturnExpectedResult(string address, bool expected)
    {
        var result = NetworkValidator.IsValidListenAddress(address);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0.0.0.0", true)]
    [InlineData("::", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("192.168.1.1", false)]
    public void IsListeningOnAllInterfaces_ShouldReturnExpectedResult(string address, bool expected)
    {
        var result = NetworkValidator.IsListeningOnAllInterfaces(address);
        result.Should().Be(expected);
    }
}
