using System.Collections.Generic;
using System.Net;

namespace PrintPilotProxy.Core.Models;

public class DiscoveredNetworkInterface
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool IsOperational { get; set; }
    public List<IPAddress> Addresses { get; set; } = new();
}
