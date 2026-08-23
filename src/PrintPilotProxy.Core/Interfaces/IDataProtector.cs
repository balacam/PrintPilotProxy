namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Provides secure encryption and decryption of sensitive data.
/// </summary>
public interface IDataProtector
{
    /// <summary>
    /// Encrypts plaintext data.
    /// </summary>
    string Protect(string plainText);

    /// <summary>
    /// Decrypts protected data.
    /// </summary>
    string Unprotect(string protectedText);
}
