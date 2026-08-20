using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Constants;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Security;

/// <summary>
/// Implements automatic zero-touch HMAC authentication between PrintPilot and PrintPilotProxy.
/// Prevents untrusted LAN applications from using PrintPilotProxy without requiring user manual input.
/// </summary>
public sealed class PrintPilotHmacAuthenticator : IProxyAuthenticator
{
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);
    private readonly ILogger<PrintPilotHmacAuthenticator> _logger;
    private readonly bool _isRequired;

    public bool IsAuthenticationRequired => _isRequired;

    public PrintPilotHmacAuthenticator(
        bool isRequired = true,
        ILogger<PrintPilotHmacAuthenticator>? logger = null)
    {
        _isRequired = isRequired;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PrintPilotHmacAuthenticator>.Instance;
    }

    public AuthenticationResult Authenticate(string? authorizationHeader, IPAddress clientIp)
    {
        if (!_isRequired)
        {
            return AuthenticationResult.Success();
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return AuthenticationResult.Failure("Missing proxy authorization header.");
        }

        var headerTrimmed = authorizationHeader.Trim();
        if (!headerTrimmed.StartsWith(DiscoveryConstants.AuthScheme, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationResult.Failure($"Unsupported authorization scheme. Expected {DiscoveryConstants.AuthScheme}.");
        }

        var parametersString = headerTrimmed.Substring(DiscoveryConstants.AuthScheme.Length).Trim();
        var parameters = ParseParameters(parametersString);

        if (!parameters.TryGetValue("v", out var versionStr) || !int.TryParse(versionStr, out var version))
        {
            return AuthenticationResult.Failure("Missing or invalid protocol version in authorization header.");
        }

        if (version != DiscoveryConstants.ProtocolVersion)
        {
            return AuthenticationResult.Failure($"Unsupported protocol version: {version}. Expected {DiscoveryConstants.ProtocolVersion}.");
        }

        if (!parameters.TryGetValue("ts", out var tsStr) || !long.TryParse(tsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            return AuthenticationResult.Failure("Missing or invalid timestamp in authorization header.");
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        var now = DateTimeOffset.UtcNow;
        var diff = (now - requestTime).Duration();
        if (diff > MaxClockSkew)
        {
            _logger.LogWarning("Authentication failed for {ClientIp}: Clock skew exceeded ({SkewSeconds}s diff).", clientIp, diff.TotalSeconds);
            return AuthenticationResult.Failure("Authorization timestamp expired or clock skew too large.");
        }

        if (!parameters.TryGetValue("nonce", out var nonce) || string.IsNullOrWhiteSpace(nonce) || nonce.Length < 6)
        {
            return AuthenticationResult.Failure("Missing or invalid nonce in authorization header.");
        }

        if (!parameters.TryGetValue("sig", out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            return AuthenticationResult.Failure("Missing signature in authorization header.");
        }

        var secret = GetProtocolSecret(version);
        var expectedSignatureHex = ComputeSignature(secret, version, timestampSeconds, nonce);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignatureHex.ToLowerInvariant());
        var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());

        if (expectedBytes.Length != actualBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            _logger.LogWarning("Authentication signature mismatch for client {ClientIp}.", clientIp);
            return AuthenticationResult.Failure("Invalid authorization signature.");
        }

        return AuthenticationResult.Success(version);
    }

    public string GenerateAuthHeader(int protocolVersion = 1, string? nonce = null, DateTimeOffset? timestamp = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var n = nonce ?? Guid.NewGuid().ToString("N");
        var secret = GetProtocolSecret(protocolVersion);
        var sig = ComputeSignature(secret, protocolVersion, ts, n);

        return $"{DiscoveryConstants.AuthScheme} v={protocolVersion},ts={ts},nonce={n},sig={sig}";
    }

    private static string ComputeSignature(byte[] secret, int version, long timestampSeconds, string nonce)
    {
        var payloadString = $"PrintPilotAuth:v={version}:ts={timestampSeconds}:nonce={nonce}";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadString);

        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] GetProtocolSecret(int protocolVersion)
    {
        // Internal obfuscated byte fragments combined and hashed with SHA-256
        // to avoid raw literal strings in binaries and memory scanning while
        // maintaining consistent secret derivation across versions.
        var p1 = new byte[] { 0x50, 0x72, 0x69, 0x6E, 0x74, 0x50, 0x69, 0x6C, 0x6F, 0x74 }; // PrintPilot
        var p2 = new byte[] { 0x50, 0x72, 0x6F, 0x78, 0x79, 0x53, 0x65, 0x63, 0x72, 0x65 }; // ProxySecre
        var p3 = new byte[] { 0x74, 0x4B, 0x65, 0x79, 0x5F, 0x56, (byte)(0x30 + protocolVersion) }; // tKey_Vx
        var p4 = new byte[] { 0xA5, 0x5A, 0xC3, 0x3C, 0x7E, 0x81, 0x9D, 0x24 }; // Salt entropy

        var combined = new byte[p1.Length + p2.Length + p3.Length + p4.Length];
        Buffer.BlockCopy(p1, 0, combined, 0, p1.Length);
        Buffer.BlockCopy(p2, 0, combined, p1.Length, p2.Length);
        Buffer.BlockCopy(p3, 0, combined, p1.Length + p2.Length, p3.Length);
        Buffer.BlockCopy(p4, 0, combined, p1.Length + p2.Length + p3.Length, p4.Length);

        return SHA256.HashData(combined);
    }

    private static Dictionary<string, string> ParseParameters(string paramString)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tokens = paramString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var eqIndex = token.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = token.Substring(0, eqIndex).Trim();
                var val = token.Substring(eqIndex + 1).Trim().Trim('"');
                dict[key] = val;
            }
        }
        return dict;
    }
}
