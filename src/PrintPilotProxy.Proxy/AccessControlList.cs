using System.Net;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.Proxy;

/// <summary>
/// Thread-safe implementation of IAccessControlList.
/// Evaluates whether a client IP and destination port are authorized
/// to use the proxy based on the configured rules.
/// </summary>
public sealed class AccessControlList : IAccessControlList
{
    private readonly object _lock = new();
    private List<AllowedClient> _allowedClients = new();
    private List<int> _allowedDestinationPorts = new() { 80, 443 };
    private bool _destinationPortRestrictionsEnabled = true;

    /// <summary>
    /// Initializes a new instance of AccessControlList.
    /// </summary>
    public AccessControlList()
    {
    }

    /// <summary>
    /// Initializes a new instance of AccessControlList with the provided configuration.
    /// </summary>
    public AccessControlList(ProxyConfiguration configuration)
    {
        Refresh(configuration);
    }

    /// <inheritdoc />
    public bool IsAllowed(IPAddress clientAddress)
    {
        lock (_lock)
        {
            if (_allowedClients.Count == 0)
                return false;

            foreach (var client in _allowedClients)
            {
                if (client.Enabled && NetworkValidator.IsMatch(clientAddress, client.IpOrCidr))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsDestinationPortAllowed(int port)
    {
        lock (_lock)
        {
            if (!_destinationPortRestrictionsEnabled)
                return true;

            return _allowedDestinationPorts.Contains(port);
        }
    }

    /// <inheritdoc />
    public void Refresh(ProxyConfiguration configuration)
    {
        lock (_lock)
        {
            _allowedClients = configuration.AllowedClients?.Where(c => c.Enabled).ToList() ?? new List<AllowedClient>();
            _allowedDestinationPorts = configuration.Security?.AllowedDestinationPorts ?? new List<int> { 80, 443 };
            _destinationPortRestrictionsEnabled = configuration.Security?.DestinationPortRestrictionsEnabled ?? true;
        }
    }
}
