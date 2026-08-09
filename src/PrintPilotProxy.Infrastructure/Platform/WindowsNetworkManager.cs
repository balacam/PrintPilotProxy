using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Platform
{
    public class WindowsNetworkManager : IPlatformNetworkManager
    {
        private readonly ILogger<WindowsNetworkManager> _logger;
        private readonly HttpClient _httpClient;

        public WindowsNetworkManager(ILogger<WindowsNetworkManager> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public IReadOnlyList<NetworkInterfaceInfo> GetInterfaces()
        {
            var interfaces = new List<NetworkInterfaceInfo>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    (ni.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var props = ni.GetIPProperties();
                    var ipv4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                    
                    if (ipv4 != null)
                    {
                        interfaces.Add(new NetworkInterfaceInfo
                        {
                            Name = ni.Name,
                            Description = ni.Description,
                            IPv4Addresses = new List<string> { ipv4.Address.ToString() },
                            IsUp = ni.OperationalStatus == OperationalStatus.Up,
                            InterfaceType = ni.NetworkInterfaceType.ToString()
                        });
                    }
                }
            }
            return interfaces;
        }

        public bool IsPortAvailable(int port, string? address = null)
        {
            try
            {
                var ipAddr = address == null ? IPAddress.Any : IPAddress.Parse(address);
                using var listener = new TcpListener(ipAddr, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking port availability for {Port} on {IpAddress}", port, address);
                return false;
            }
        }

        public async Task<bool> TestDnsResolutionAsync(string hostName, CancellationToken cancellationToken = default)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(hostName, cancellationToken);
                return addresses.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DNS resolution failed for {HostName}", hostName);
                return false;
            }
        }

        public async Task<bool> TestInternetConnectivityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://dns.google", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Internet connectivity test failed.");
                return false;
            }
        }
    }
}
