using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Security;

public sealed class DpapiDataProtector : IDataProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PrintPilotProxy");
    private readonly ILogger<DpapiDataProtector> _logger;

    public DpapiDataProtector(ILogger<DpapiDataProtector>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DpapiDataProtector>.Instance;
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        // We use DataProtectionScope.LocalMachine for services that run as system or specific user,
        // but since configuration might be written by UI (User) and read by Service (System), LocalMachine is required.
        var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return string.Empty;

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedText);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "DPAPI Unprotect failed. The protected data may have been encrypted on a different machine or by a different user. Returning empty string.");
            return string.Empty;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "DPAPI Unprotect failed due to invalid Base64 format. Returning empty string.");
            return string.Empty;
        }
    }
}
