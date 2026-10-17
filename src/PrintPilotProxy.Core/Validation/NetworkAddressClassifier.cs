using System.Net;
using System.Net.Sockets;

namespace PrintPilotProxy.Core.Validation;

/// <summary>Tells whether an address a proxy listens on is reachable from the public internet.</summary>
public static class NetworkAddressClassifier
{
    /// <summary>
    /// True for globally routable addresses: public IPv4 and IPv6 global unicast. False for loopback, private
    /// (RFC1918), carrier-grade NAT (100.64/10), link-local, IPv6 unique-local and similar non-public ranges.
    /// </summary>
    public static bool IsPubliclyRoutable(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 169 && b[1] == 254)
                || b[0] == 0
                || b[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            {
                return false;
            }

            var first = address.GetAddressBytes()[0];
            // fc00::/7 is unique-local; 2000::/3 is global unicast.
            return (first & 0xFE) != 0xFC && (first & 0xE0) == 0x20;
        }

        return false;
    }
}
