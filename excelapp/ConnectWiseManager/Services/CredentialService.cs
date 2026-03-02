using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConnectWiseManager.Models;
using Newtonsoft.Json;

namespace ConnectWiseManager.Services;

public class CredentialService : ICredentialService
{
    private readonly string _credentialsPath;
    private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("ConnectWiseManager_SecureKey_v1");

    public CredentialService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConnectWiseManager");
        
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }
        
        _credentialsPath = Path.Combine(appDataPath, "credentials.dat");
    }

    public async Task SaveCredentialsAsync(ApiCredentials credentials)
    {
        try
        {
            var json = JsonConvert.SerializeObject(credentials);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            
            // Encrypt data using DPAPI (Windows Data Protection API)
            var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
            
            await File.WriteAllBytesAsync(_credentialsPath, encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save credentials", ex);
        }
    }

    public async Task<ApiCredentials?> LoadCredentialsAsync()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                return null;
            }

            var encryptedBytes = await File.ReadAllBytesAsync(_credentialsPath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            
            return JsonConvert.DeserializeObject<ApiCredentials>(json);
        }
        catch (Exception)
        {
            // If decryption fails, return null
            return null;
        }
    }

    public Task ClearCredentialsAsync()
    {
        try
        {
            if (File.Exists(_credentialsPath))
            {
                File.Delete(_credentialsPath);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to clear credentials", ex);
        }
    }

    public bool HasStoredCredentials()
    {
        return File.Exists(_credentialsPath);
    }
}
