using System.Net.Http.Headers;
using System.Text;
using CwAssetManager.Core.Models;

namespace CwAssetManager.Infrastructure.Auth;

/// <summary>
/// Provides API key / public-private key authentication for ConnectWise Manage.
/// Builds the Authorization: Basic header expected by CW Manage.
/// </summary>
public sealed class ApiKeyAuthProvider
{
    private readonly AuthConfig _config;

    public ApiKeyAuthProvider(AuthConfig config)
        => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>Returns the CW Manage Basic auth header value.</summary>
    public AuthenticationHeaderValue GetManageBasicHeader()
    {
        // CW Manage expects:  CompanyId+publicKey:privateKey  (Base64)
        var raw = $"{_config.CompanyId}+{_config.ApiPublicKey}:{_config.ApiPrivateKey}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return new AuthenticationHeaderValue("Basic", b64);
    }

    /// <summary>Returns the clientId header value required by CW Manage.</summary>
    public string GetManageClientId()
        => _config.ApiClientId ?? throw new InvalidOperationException("ApiClientId is not configured.");

    /// <summary>Returns a raw API key for RMM / Asio key-header auth.</summary>
    public string GetRawApiKey()
        => _config.ApiKey ?? throw new InvalidOperationException("ApiKey is not configured.");

    /// <summary>Returns the configured API key header name (default: x-api-key).</summary>
    public string GetApiKeyHeaderName()
        => _config.ApiKeyHeader ?? "x-api-key";
}
