using ConnectWiseManager.Models;

namespace ConnectWiseManager.Services;

public interface ICredentialService
{
    Task SaveCredentialsAsync(ApiCredentials credentials);
    Task<ApiCredentials?> LoadCredentialsAsync();
    Task ClearCredentialsAsync();
    bool HasStoredCredentials();
}
