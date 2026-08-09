using System.Net;
using System.Net.Sockets;

namespace PrintPilotProxy.Core.Validation;

/// <summary>
/// Validates IP addresses and CIDR notation.
/// </summary>
public static class NetworkValidator
{
    /// <summary>
    /// Validates whether the string is a valid IPv4 or IPv6 address.
    /// </summary>
    public static bool IsValidIpAddress(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return IPAddress.TryParse(input.Trim(), out _);
    }

    /// <summary>
    /// Validates whether the string is a valid CIDR notation (e.g., "192.168.10.0/24").
    /// </summary>
    public static bool IsValidCidr(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex < 0)
            return false;

        var ipPart = trimmed[..slashIndex];
        var prefixPart = trimmed[(slashIndex + 1)..];

        if (!IPAddress.TryParse(ipPart, out var address))
            return false;

        if (!int.TryParse(prefixPart, out var prefixLength))
            return false;

        var maxPrefix = address.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;

        return prefixLength >= 0 && prefixLength <= maxPrefix;
    }

    /// <summary>
    /// Validates whether the string is a valid IP address or CIDR.
    /// </summary>
    public static bool IsValidIpOrCidr(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return input.Contains('/') ? IsValidCidr(input) : IsValidIpAddress(input);
    }

    /// <summary>
    /// Checks if a given IP address matches a CIDR range.
    /// </summary>
    public static bool IsInCidrRange(IPAddress address, string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var slashIndex = cidr.IndexOf('/');
        if (slashIndex < 0)
        {
            // Exact IP match
            return IPAddress.TryParse(cidr, out var exactIp) && address.Equals(exactIp);
        }

        var networkPart = cidr[..slashIndex];
        var prefixPart = cidr[(slashIndex + 1)..];

        if (!IPAddress.TryParse(networkPart, out var networkAddress))
            return false;

        if (!int.TryParse(prefixPart, out var prefixLength))
            return false;

        // Address families must match
        if (address.AddressFamily != networkAddress.AddressFamily)
            return false;

        var addressBytes = address.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();

        // Compare bit by bit up to prefix length
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
                return false;
        }

        if (remainingBits > 0 && fullBytes < addressBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((addressBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a given IP address matches an IP or CIDR entry.
    /// </summary>
    public static bool IsMatch(IPAddress address, string ipOrCidr)
    {
        if (string.IsNullOrWhiteSpace(ipOrCidr))
            return false;

        return ipOrCidr.Contains('/')
            ? IsInCidrRange(address, ipOrCidr)
            : IPAddress.TryParse(ipOrCidr.Trim(), out var ip) && address.Equals(ip);
    }

    /// <summary>
    /// Determines if a CIDR range is considered "broad" and potentially risky.
    /// Returns a warning message if broad, null otherwise.
    /// </summary>
    public static string? GetBroadSubnetWarning(string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr) || !cidr.Contains('/'))
            return null;

        var prefixPart = cidr[(cidr.IndexOf('/') + 1)..];
        if (!int.TryParse(prefixPart, out var prefixLength))
            return null;

        var ipPart = cidr[..cidr.IndexOf('/')];
        if (!IPAddress.TryParse(ipPart, out var address))
            return null;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            if (prefixLength == 0)
                return $"CIDR {cidr} allows ALL IPv4 addresses. This creates an open proxy.";
            if (prefixLength <= 8)
                return $"CIDR {cidr} allows approximately {Math.Pow(2, 32 - prefixLength):N0} addresses. This is a very broad range.";
            if (prefixLength <= 16)
                return $"CIDR {cidr} allows approximately {Math.Pow(2, 32 - prefixLength):N0} addresses. This is a broad range.";
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (prefixLength == 0)
                return $"CIDR {cidr} allows ALL IPv6 addresses. This creates an open proxy.";
            if (prefixLength <= 48)
                return $"CIDR {cidr} is a very broad IPv6 range.";
        }

        return null;
    }

    /// <summary>
    /// Validates a port number.
    /// </summary>
    public static bool IsValidPort(int port) => port >= 1 && port <= 65535;

    /// <summary>
    /// Validates a listen address (must be a valid IP or 0.0.0.0).
    /// </summary>
    public static bool IsValidListenAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        return IPAddress.TryParse(address.Trim(), out _);
    }

    /// <summary>
    /// Returns whether the listen address is configured to listen on all interfaces.
    /// </summary>
    public static bool IsListeningOnAllInterfaces(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        var trimmed = address.Trim();
        return trimmed == "0.0.0.0" || trimmed == "::" || trimmed == "[::]";
    }
}
