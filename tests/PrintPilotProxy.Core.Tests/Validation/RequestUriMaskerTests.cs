using System;
using FluentAssertions;
using PrintPilotProxy.Core.Validation;
using Xunit;

namespace PrintPilotProxy.Core.Tests.Validation;

public class RequestUriMaskerTests
{
    [Theory]
    [InlineData("http://example.com/path?token=secret&x=1", "http://example.com/path?[redacted]")]
    [InlineData("http://user:pass@example.com:8080/a/b", "http://example.com:8080/a/b")]
    [InlineData("https://example.com/a#frag", "https://example.com/a")]
    [InlineData("http://example.com", "http://example.com/")]
    public void Mask_RemovesSecretBearingParts(string input, string expected)
    {
        RequestUriMasker.Mask(new Uri(input)).Should().Be(expected);
    }

    [Fact]
    public void Mask_NullUri_ReturnsUnknown()
    {
        RequestUriMasker.Mask(null).Should().Be("Unknown");
    }

    [Fact]
    public void Mask_RelativeUri_DropsTheQuery()
    {
        RequestUriMasker.Mask(new Uri("/path?k=v", UriKind.Relative)).Should().Be("/path?[redacted]");
    }
}
