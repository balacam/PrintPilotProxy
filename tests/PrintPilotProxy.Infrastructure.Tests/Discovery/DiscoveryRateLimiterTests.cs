using System;
using System.Net;
using FluentAssertions;
using PrintPilotProxy.Infrastructure.Discovery;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Discovery;

public class DiscoveryRateLimiterTests
{
    [Fact]
    public void ShouldAllow_UnderLimit_ReturnsTrue()
    {
        var limiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 5, windowDuration: TimeSpan.FromSeconds(2));
        var ip = IPAddress.Parse("192.168.1.50");

        for (int i = 0; i < 5; i++)
        {
            limiter.ShouldAllow(ip).Should().BeTrue();
        }
    }

    [Fact]
    public void ShouldAllow_ExceedsLimit_ReturnsFalse()
    {
        var limiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 3, windowDuration: TimeSpan.FromSeconds(2));
        var ip = IPAddress.Parse("192.168.1.50");

        limiter.ShouldAllow(ip).Should().BeTrue();
        limiter.ShouldAllow(ip).Should().BeTrue();
        limiter.ShouldAllow(ip).Should().BeTrue();

        // 4th request in the same window must be dropped
        limiter.ShouldAllow(ip).Should().BeFalse();
        limiter.ShouldAllow(ip).Should().BeFalse();
    }

    [Fact]
    public void ShouldAllow_DifferentIps_TrackedSeparately()
    {
        var limiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 2, windowDuration: TimeSpan.FromSeconds(2));
        var ip1 = IPAddress.Parse("192.168.1.50");
        var ip2 = IPAddress.Parse("192.168.1.51");

        limiter.ShouldAllow(ip1).Should().BeTrue();
        limiter.ShouldAllow(ip1).Should().BeTrue();
        limiter.ShouldAllow(ip1).Should().BeFalse();

        // ip2 should still have its full quota
        limiter.ShouldAllow(ip2).Should().BeTrue();
        limiter.ShouldAllow(ip2).Should().BeTrue();
        limiter.ShouldAllow(ip2).Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsRateLimitState()
    {
        var limiter = new DiscoveryRateLimiter(maxRequestsPerWindow: 1, windowDuration: TimeSpan.FromSeconds(5));
        var ip = IPAddress.Parse("192.168.1.50");

        limiter.ShouldAllow(ip).Should().BeTrue();
        limiter.ShouldAllow(ip).Should().BeFalse();

        limiter.Reset();
        limiter.ShouldAllow(ip).Should().BeTrue();
    }
}
