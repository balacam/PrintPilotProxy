using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Validation;

/// <summary>
/// Validates ProxyConfiguration and its components.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates the entire proxy configuration and returns a list of error messages.
    /// An empty list means the configuration is valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(ProxyConfiguration config)
    {
        var errors = new List<string>();

        // Listener validation
        ValidateListener(config.Listener, errors);

        // Allowed clients validation
        ValidateClientAccess(config.ClientAccess, errors);

        // Security validation
        ValidateSecurity(config.Security, errors);

        // Logging validation
        ValidateLogging(config.Logging, errors);

        // Service validation
        ValidateService(config.Service, errors);

        return errors;
    }

    private static void ValidateListener(ListenerSettings listener, List<string> errors)
    {
        if (listener.Mode == ListenerMode.SpecificAddress)
        {
            if (string.IsNullOrWhiteSpace(listener.ListenAddress))
            {
                errors.Add("Listen address must be provided when mode is SpecificAddress.");
            }
            else if (!NetworkValidator.IsValidListenAddress(listener.ListenAddress))
            {
                errors.Add($"Listen address '{listener.ListenAddress}' is not a valid IP address.");
            }
        }

        if (listener.Mode == ListenerMode.SpecificAdapter && string.IsNullOrWhiteSpace(listener.AdapterName))
        {
            errors.Add("A network adapter must be selected when mode is SpecificAdapter.");
        }

        if (!NetworkValidator.IsValidPort(listener.Port))
        {
            errors.Add($"Port {listener.Port} is not valid. Must be between 1 and 65535.");
        }

        if (listener.MaxConnections < 1 || listener.MaxConnections > 100000)
        {
            errors.Add($"MaxConnections {listener.MaxConnections} is out of range (1-100000).");
        }

        if (listener.ConnectionTimeoutSeconds < 1 || listener.ConnectionTimeoutSeconds > 3600)
        {
            errors.Add($"ConnectionTimeoutSeconds {listener.ConnectionTimeoutSeconds} is out of range (1-3600).");
        }
    }

    private static void ValidateClientAccess(ClientAccessSettings clientAccess, List<string> errors)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var client in clientAccess.AllowedClients)
        {
            if (string.IsNullOrWhiteSpace(client.Name))
            {
                errors.Add($"Allowed client with ID '{client.Id}' has an empty name.");
            }
            else if (!seenNames.Add(client.Name))
            {
                errors.Add($"Duplicate allowed client name: '{client.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(client.IpOrCidr))
            {
                errors.Add($"Allowed client '{client.Name}' has an empty IP/CIDR value.");
            }
            else if (!NetworkValidator.IsValidIpOrCidr(client.IpOrCidr))
            {
                errors.Add($"Allowed client '{client.Name}' has an invalid IP/CIDR: '{client.IpOrCidr}'.");
            }
        }
    }

    private static void ValidateSecurity(SecuritySettings security, List<string> errors)
    {
        if (security.DestinationPortRestrictionsEnabled)
        {
            if (security.AllowedDestinationPorts.Count == 0)
            {
                errors.Add("Destination port restrictions are enabled but no ports are configured.");
            }

            foreach (var port in security.AllowedDestinationPorts)
            {
                if (!NetworkValidator.IsValidPort(port))
                {
                    errors.Add($"Allowed destination port {port} is not valid.");
                }
            }
        }
    }

    private static void ValidateLogging(LoggingSettings logging, List<string> errors)
    {
        if (logging.RetentionDays < 1 || logging.RetentionDays > 365)
        {
            errors.Add($"Log retention {logging.RetentionDays} days is out of range (1-365).");
        }

        if (logging.MaxSizeMb < 1 || logging.MaxSizeMb > 10000)
        {
            errors.Add($"Log max size {logging.MaxSizeMb} MB is out of range (1-10000).");
        }
    }

    private static void ValidateService(ServiceSettings service, List<string> errors)
    {
        if (service.RestartDelaySeconds < 1 || service.RestartDelaySeconds > 300)
        {
            errors.Add($"Restart delay {service.RestartDelaySeconds}s is out of range (1-300).");
        }
    }

    /// <summary>
    /// Gets security warnings for the configuration (non-fatal issues).
    /// </summary>
    public static IReadOnlyList<string> GetWarnings(ProxyConfiguration config)
    {
        var warnings = new List<string>();

        // Warn about listening on all interfaces
        if (config.Listener.Mode == ListenerMode.AllInterfaces || 
            (config.Listener.Mode == ListenerMode.SpecificAddress && config.Listener.ListenAddress != null && NetworkValidator.IsListeningOnAllInterfaces(config.Listener.ListenAddress)))
        {
            warnings.Add("Proxy is configured to listen on all interfaces. Consider binding to a specific IP address.");
        }

        // Warn about no allowed clients in AllowList mode
        if (config.ClientAccess.Mode == ClientAccessMode.AllowList && config.ClientAccess.AllowedClients.Count == 0)
        {
            warnings.Add("No allowed clients are configured. No client will be able to use the proxy.");
        }
        
        // Warn about AllowAll mode
        if (config.ClientAccess.Mode == ClientAccessMode.AllowAll)
        {
            warnings.Add("Client access is set to AllowAll. Anyone can use the proxy.");
        }

        // Warn about no enabled clients
        if (config.ClientAccess.Mode == ClientAccessMode.AllowList && config.ClientAccess.AllowedClients.Count > 0 && !config.ClientAccess.AllowedClients.Any(c => c.Enabled))
        {
            warnings.Add("All allowed clients are disabled. No client will be able to use the proxy.");
        }

        // Warn about broad subnets
        foreach (var client in config.ClientAccess.AllowedClients.Where(c => c.Enabled))
        {
            var warning = NetworkValidator.GetBroadSubnetWarning(client.IpOrCidr);
            if (warning != null)
            {
                warnings.Add($"Client '{client.Name}': {warning}");
            }
        }

        // Warn about disabled port restrictions
        if (!config.Security.DestinationPortRestrictionsEnabled)
        {
            warnings.Add("Destination port restrictions are disabled. The proxy will forward traffic to any port.");
        }

        return warnings;
    }
}
