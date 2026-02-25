namespace CwAssetManager.Core.Models;

/// <summary>OAuth2 and API key/secret authentication configuration for a provider.</summary>
public sealed class AuthConfig
{
    /// <summary>Base URL for the API (e.g. https://manage.example.com/v4_6_release/apis/3.0).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    // --- API Key / Secret (Manage, RMM) ---
    public string? ApiClientId { get; set; }
    public string? ApiPublicKey { get; set; }
    public string? ApiPrivateKey { get; set; }
    public string? CompanyId { get; set; }

    // --- OAuth2 Client Credentials (Control) ---
    public string? OAuthClientId { get; set; }
    public string? OAuthClientSecret { get; set; }
    public string? OAuthTokenEndpoint { get; set; }
    public string? OAuthScope { get; set; }

    // --- Generic API key header ---
    public string? ApiKey { get; set; }
    public string? ApiKeyHeader { get; set; } = "x-api-key";
}
