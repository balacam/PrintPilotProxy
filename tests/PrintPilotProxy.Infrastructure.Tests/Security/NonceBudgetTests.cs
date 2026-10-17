using System.Net;
using FluentAssertions;
using PrintPilotProxy.Infrastructure.Security;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Security;

/// <summary>PROXY-010: the replay cache has a hard size and nonce-length bound.</summary>
public class NonceBudgetTests
{
    private static readonly IPAddress Client = IPAddress.Parse("192.168.1.10");

    [Fact]
    public void ValidRequests_AreAcceptedUpToTheBudget_ThenRefused()
    {
        var auth = new PrintPilotHmacAuthenticator(isRequired: true, logger: null, maxTrackedNonces: 3);

        for (var i = 0; i < 3; i++)
        {
            auth.Authenticate(auth.GenerateAuthHeader(nonce: $"nonce-budget-{i}"), Client).IsSuccess.Should().BeTrue();
        }

        var refused = auth.Authenticate(auth.GenerateAuthHeader(nonce: "nonce-budget-overflow"), Client);
        refused.IsSuccess.Should().BeFalse();
        refused.FailureReason.Should().Contain("Too many");
    }

    [Fact]
    public void OversizedNonce_IsRejected()
    {
        var auth = new PrintPilotHmacAuthenticator();
        var header = auth.GenerateAuthHeader(nonce: new string('a', 129));
        auth.Authenticate(header, Client).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ReplayedNonce_IsStillRejected()
    {
        var auth = new PrintPilotHmacAuthenticator();
        var header = auth.GenerateAuthHeader(nonce: "replay-check-1");
        auth.Authenticate(header, Client).IsSuccess.Should().BeTrue();
        auth.Authenticate(header, Client).IsSuccess.Should().BeFalse();
    }
}
