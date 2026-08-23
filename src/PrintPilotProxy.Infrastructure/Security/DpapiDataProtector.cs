using System;
using System.Security.Cryptography;
using System.Text;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Security;

public sealed class DpapiDataProtector : IDataProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PrintPilotProxy");

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
        catch (CryptographicException)
        {
            // If it can't be decrypted, return empty or handle safely
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
