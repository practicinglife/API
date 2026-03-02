namespace ConnectWiseManager.Models;

public class ApiCredentials
{
    public AsioCredentials? Asio { get; set; }
    public ScreenConnectCredentials? ScreenConnect { get; set; }
    public ReportingCredentials? Reporting { get; set; }
}

public class AsioCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    // Optional audience depending on the OAuth2 server configuration
    public string? Audience { get; set; }
    // Allow overriding scopes if tenant requires different permissions
    public string Scope { get; set; } = "platform.asset.read platform.devices.read platform.companies.read platform.sites.read platform.custom_fields_definitions.read";
    // Optional explicit token endpoint. If not set, the service will attempt discovery and common fallbacks.
    public string? TokenEndpoint { get; set; }
    // Whether to send client credentials in Authorization: Basic header as well as in body (for broader compatibility)
    public bool UseBasicClientAuth { get; set; } = true;
    // New: optional override for devices endpoint path (e.g., "/v1/devices" or full URL)
    public string? DevicesEndpointPath { get; set; }
}

public class ScreenConnectCredentials
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    // If using Personal Access Token (recommended), supply it and we will use X-Auth-Token
    public string? PersonalAccessToken { get; set; }
    public string? AuthToken { get; set; }
    // Optional TOTP/email code when using Basic + MFA
    public string? OneTimePassword { get; set; }
    // Allow overriding sessions endpoint path (e.g., "/api/sessions" or full URL)
    public string? SessionsEndpointPath { get; set; }
    public bool ShouldTrust { get; set; } = true;

    // Optional: custom header-based auth for App_Extensions APIs (e.g., AuthenticationSecret)
    public string? CustomAuthHeaderName { get; set; }
    public string? CustomAuthHeaderValue { get; set; }
}

/// <summary>
/// Credentials for the IT Glue / Asio Reporting API (API key only, no OAuth).
/// </summary>
public class ReportingCredentials
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://itsapi.itsupport247.net";
}
