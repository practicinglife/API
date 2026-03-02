using ConnectWiseManager.Models;

namespace ConnectWiseManager.Services;

public interface IScreenConnectApiService
{
    Task<bool> AuthenticateAsync(ScreenConnectCredentials credentials);
    Task<bool> SendCommandToSessionAsync(string sessionId, string command);
    Task<bool> DeployInstallerAsync(string sessionId, string installerUrl);
    Task<bool> ExecuteRepairScriptAsync(string sessionId);
    Task<ScriptExecution?> GetSessionDetailsAsync(string sessionId);
    Task<bool> AddNoteToSessionAsync(string sessionId, string note);
    Task<bool> CreateSessionAsync(string deviceName, Dictionary<string, string> customProperties);
    Task<bool> SendMessageToSessionAsync(string sessionId, string message);
    Task<bool> UpdateSessionCustomPropertiesAsync(string sessionId, Dictionary<string, string> properties);
    Task<List<ScreenConnectSession>> GetSessionsAsync();

    // Bulk update sessions via UpdateAllSessions endpoint
    // authHeaderName/authHeaderValue allow header-only auth where the extension expects a custom header (e.g., "AuthenticationSecret")
    Task<BulkUpdateResponse?> UpdateAllSessionsAsync(
        IEnumerable<UpdateSessionInstruction> sessions,
        string? returnDataCsv = null,
        string? endpointPathOrUrl = null,
        string? authHeaderName = null,
        string? authHeaderValue = null,
        CancellationToken ct = default);
}
