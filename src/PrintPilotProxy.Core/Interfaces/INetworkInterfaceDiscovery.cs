using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

public interface INetworkInterfaceDiscovery
{
    Task<IEnumerable<DiscoveredNetworkInterface>> GetInterfacesAsync();
    Task<IEnumerable<IPAddress>> GetUsablePrivateIpAddressesAsync();
}
