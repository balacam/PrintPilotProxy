using System;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Infrastructure.Security;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Security;

public class ProxyAuthenticationTests
{
    private readonly PrintPilotHmacAuthenticator _authenticator;
    private readonly IPAddress _clientIp = IPAddress.Parse("192.168.1.50");

    public ProxyAuthenticationTests()
    {
        _authenticator = new PrintPilotHmacAuthenticator(
            isRequired: true,
            logger: NullLogger<PrintPilotHmacAuthenticator>.Instance);
    }

    [Fact]
    public void Authenticate_ValidGeneratedHeader_Succeeds()
    {
        var authHeader = _authenticator.GenerateAuthHeader(protocolVersion: 1);

        var result = _authenticator.Authenticate(authHeader, _clientIp);

        result.IsSuccess.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.ProtocolVersion.Should().Be(1);
    }

    [Fact]
    public void Authenticate_MissingHeader_Fails()
    {
        var result = _authenticator.Authenticate(null, _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Missing");
    }

    [Fact]
    public void Authenticate_InvalidScheme_Fails()
    {
        var result = _authenticator.Authenticate("Basic dXNlcjpwYXNz", _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Unsupported authorization scheme");
    }

    [Fact]
    public void Authenticate_TamperedSignature_Fails()
    {
        var validHeader = _authenticator.GenerateAuthHeader(protocolVersion: 1);
        var tamperedHeader = validHeader.Substring(0, validHeader.Length - 4) + "ffff";

        var result = _authenticator.Authenticate(tamperedHeader, _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Invalid authorization signature");
    }

    [Fact]
    public void Authenticate_ExpiredTimestamp_ReplayProtection_Fails()
    {
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var expiredHeader = _authenticator.GenerateAuthHeader(protocolVersion: 1, timestamp: expiredTime);

        var result = _authenticator.Authenticate(expiredHeader, _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("expired or clock skew");
    }

    [Fact]
    public void Authenticate_FutureTimestampBeyondSkew_Fails()
    {
        var futureTime = DateTimeOffset.UtcNow.AddMinutes(10);
        var futureHeader = _authenticator.GenerateAuthHeader(protocolVersion: 1, timestamp: futureTime);

        var result = _authenticator.Authenticate(futureHeader, _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("expired or clock skew");
    }

    [Fact]
    public void Authenticate_UnsupportedProtocolVersion_Fails()
    {
        var header = "PrintPilot-HMAC v=99,ts=1724050000,nonce=abcdef123,sig=123456";

        var result = _authenticator.Authenticate(header, _clientIp);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("Unsupported protocol version");
    }

    [Fact]
    public void Authenticate_WhenNotRequired_AlwaysSucceeds()
    {
        var optionalAuth = new PrintPilotHmacAuthenticator(isRequired: false);

        var result = optionalAuth.Authenticate(null, _clientIp);

        result.IsSuccess.Should().BeTrue();
    }
}
