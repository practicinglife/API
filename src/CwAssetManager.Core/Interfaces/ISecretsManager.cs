namespace CwAssetManager.Core.Interfaces;

/// <summary>Secure secrets manager - encrypts and stores sensitive configuration values.</summary>
public interface ISecretsManager
{
    /// <summary>Stores a secret value under the given key, encrypted at rest.</summary>
    Task SetSecretAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Retrieves and decrypts a secret by key.</summary>
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);

    /// <summary>Removes a secret by key.</summary>
    Task DeleteSecretAsync(string key, CancellationToken ct = default);

    /// <summary>Returns true if a secret exists for the given key.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
