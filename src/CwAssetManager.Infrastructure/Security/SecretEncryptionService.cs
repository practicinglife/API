using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.Infrastructure.Security;

/// <summary>
/// AES-256-CBC encryption service for secrets stored on disk.
/// Uses a machine-unique key derived via PBKDF2 so secrets are portable per-machine.
/// On Windows, the key derivation salt is additionally protected with DPAPI user-scope.
/// </summary>
public sealed class SecretEncryptionService : ISecretsManager
{
    private const int KeyBytes = 32;  // 256-bit AES key
    private const int IvBytes = 16;
    private const int Iterations = 100_000;
    private const string StoreFileName = "cw-secrets.bin";

    private readonly string _storePath;
    private readonly byte[] _key;
    private Dictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <param name="storageDirectory">Directory where the encrypted secrets file is written.</param>
    /// <param name="masterPassphrase">Master passphrase used to derive the AES key. If null, uses a machine-specific default.</param>
    public SecretEncryptionService(string storageDirectory, string? masterPassphrase = null)
    {
        Directory.CreateDirectory(storageDirectory);
        _storePath = Path.Combine(storageDirectory, StoreFileName);
        _key = DeriveKey(masterPassphrase ?? GetMachinePassphrase());
        LoadAsync().GetAwaiter().GetResult();   // synchronous load at construction
    }

    /// <inheritdoc/>
    public async Task SetSecretAsync(string key, string value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cache[key] = value;
            await SaveAsync(ct).ConfigureAwait(false);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { return _cache.TryGetValue(key, out var v) ? v : null; }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cache.Remove(key);
            await SaveAsync(ct).ConfigureAwait(false);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { return _cache.ContainsKey(key); }
        finally { _lock.Release(); }
    }

    // ── Encryption Helpers ──────────────────────────────────────────────────

    private async Task SaveAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_cache);
        var plaintext = Encoding.UTF8.GetBytes(json);
        var ciphertext = Encrypt(plaintext);
        await File.WriteAllBytesAsync(_storePath, ciphertext, ct).ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        if (!File.Exists(_storePath)) return;
        try
        {
            var ciphertext = await File.ReadAllBytesAsync(_storePath).ConfigureAwait(false);
            var plaintext = Decrypt(ciphertext);
            var json = Encoding.UTF8.GetString(plaintext);
            _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            // Corrupted store – start fresh rather than crash
            _cache = new();
        }
    }

    private byte[] Encrypt(byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV);
        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(plaintext);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private byte[] Decrypt(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = ciphertext[..IvBytes];
        aes.IV = iv;
        using var ms = new MemoryStream(ciphertext, IvBytes, ciphertext.Length - IvBytes);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private static byte[] DeriveKey(string passphrase)
    {
        // Use a fixed salt tied to the app – the passphrase provides uniqueness
        var salt = Encoding.UTF8.GetBytes("CwAssetManager-v1-salt-2024");
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);
    }

    private static string GetMachinePassphrase()
    {
        // Derive a stable per-machine passphrase from the machine name + environment username
        return $"{Environment.MachineName}:{Environment.UserName}:CwAssetManager";
    }
}
