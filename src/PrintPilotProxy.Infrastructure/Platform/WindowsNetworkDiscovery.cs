using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform;

public class WindowsNetworkDiscovery : INetworkInterfaceDiscovery
{
    public Task<IEnumerable<DiscoveredNetworkInterface>> GetInterfacesAsync()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(ni =>
            {
                var addresses = ni.GetIPProperties().UnicastAddresses
                    .Select(ua => ua.Address)
                    .Where(IsUsableLocalAddress)
                    .ToList();

                var isPrivate = addresses.Any(IsPrivateIp);

                return new DiscoveredNetworkInterface
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    InterfaceType = ni.NetworkInterfaceType.ToString(),
                    IsOperational = true,
                    IsPrivate = isPrivate,
                    Addresses = addresses
                };
            });

        return Task.FromResult<IEnumerable<DiscoveredNetworkInterface>>(interfaces.ToList());
    }

    public async Task<IEnumerable<IPAddress>> GetUsablePrivateIpAddressesAsync()
    {
        var interfaces = await GetInterfacesAsync();
        return interfaces
            // The historical interface name is kept for binary compatibility.
            // Automatic mode intentionally supports any usable local address,
            // including networks that do not use RFC1918 IPv4 ranges.
            .SelectMany(ni => ni.Addresses)
            .Where(IsUsableLocalAddress)
            .ToList();
    }

    private static bool IsUsableLocalAddress(IPAddress ipAddress)
    {
        if (IPAddress.Any.Equals(ipAddress) || IPAddress.IPv6Any.Equals(ipAddress) ||
            IPAddress.None.Equals(ipAddress) || IPAddress.IPv6None.Equals(ipAddress) ||
            IPAddress.IsLoopback(ipAddress) || ipAddress.IsIPv6Multicast)
        {
            return false;
        }

        return ipAddress.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => IsUsableIpv4Address(ipAddress.GetAddressBytes()),
            System.Net.Sockets.AddressFamily.InterNetworkV6 => !ipAddress.IsIPv6LinkLocal,
            _ => false
        };
    }

    private static bool IsUsableIpv4Address(byte[] bytes)
        => bytes[0] is not (>= 224 and <= 239) && !(bytes[0] == 169 && bytes[1] == 254);

    private static bool IsPrivateIp(IPAddress ipAddress)
    {
        if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        byte[] ipBytes = ipAddress.GetAddressBytes();
        
        // 10.0.0.0/8
        if (ipBytes[0] == 10) return true;
        
        // 172.16.0.0/12
        if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) return true;
        
        // 192.168.0.0/16
        if (ipBytes[0] == 192 && ipBytes[1] == 168) return true;
        
        return false;
    }
}
