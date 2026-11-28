using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Security;

/// <summary>
/// <see cref="IDataProtector"/> for platforms without DPAPI. Secrets are sealed with AES-256-GCM under a random
/// key stored in the data directory with owner-only permissions (0600), which is the same trust boundary DPAPI
/// LocalMachine gives on Windows: anyone who can read the key file can read the secrets. The key is created on
/// first use. Losing it makes existing protected values unreadable (they unprotect to an empty string, like DPAPI
/// on another machine), so the key file belongs in backups of the data directory.
/// </summary>
public sealed class KeyFileDataProtector : IDataProtector
{
    private const string Prefix = "v1:";
    private const int KeySize = 32, NonceSize = 12, TagSize = 16;
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("PrintPilotProxy");

    private readonly string _keyPath;
    private readonly ILogger<KeyFileDataProtector> _logger;
    private readonly object _lock = new();
    private byte[]? _key;

    public KeyFileDataProtector(IPlatformPathProvider paths, ILogger<KeyFileDataProtector>? logger = null)
        : this(Path.Combine(paths.DataDirectory, "secret.key"), logger)
    {
    }

    public KeyFileDataProtector(string keyPath, ILogger<KeyFileDataProtector>? logger = null)
    {
        _keyPath = keyPath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KeyFileDataProtector>.Instance;
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(GetKey(create: true)!, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag, AssociatedData);
        }

        var blob = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        tag.CopyTo(blob, NonceSize);
        cipher.CopyTo(blob, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(blob);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        try
        {
            if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                _logger.LogWarning("Protected value is not in the key-file format (it may come from another platform). Returning empty string.");
                return string.Empty;
            }

            var blob = Convert.FromBase64String(protectedText[Prefix.Length..]);
            if (blob.Length < NonceSize + TagSize)
            {
                return string.Empty;
            }

            var key = GetKey(create: false);
            if (key is null)
            {
                _logger.LogWarning("Protection key {Path} does not exist; the protected value cannot be read. Returning empty string.", _keyPath);
                return string.Empty;
            }

            var cipher = blob.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(blob.AsSpan(0, NonceSize), cipher, blob.AsSpan(NonceSize, TagSize), plain, AssociatedData);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "Unprotect failed: wrong key or damaged value. Returning empty string.");
            return string.Empty;
        }
    }

    private byte[]? GetKey(bool create)
    {
        lock (_lock)
        {
            if (_key is not null)
            {
                return _key;
            }

            if (File.Exists(_keyPath))
            {
                var existing = Convert.FromBase64String(File.ReadAllText(_keyPath).Trim());
                if (existing.Length != KeySize)
                {
                    throw new CryptographicException($"The protection key file {_keyPath} is damaged.");
                }

                return _key = existing;
            }

            if (!create)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
            var key = RandomNumberGenerator.GetBytes(KeySize);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            try
            {
                using var stream = new FileStream(_keyPath, options);
                stream.Write(Encoding.ASCII.GetBytes(Convert.ToBase64String(key)));
            }
            catch (IOException) when (File.Exists(_keyPath))
            {
                // Another process created the key first; use theirs.
                return _key = Convert.FromBase64String(File.ReadAllText(_keyPath).Trim());
            }

            return _key = key;
        }
    }
}
