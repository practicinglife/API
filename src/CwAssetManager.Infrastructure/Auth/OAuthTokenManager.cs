using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.Infrastructure.Auth;

/// <summary>
/// Manages OAuth2 client-credentials token acquisition, caching, and refresh.
/// </summary>
public sealed class OAuthTokenManager : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuthTokenManager> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private bool _disposed;

    public OAuthTokenManager(HttpClient httpClient, ILogger<OAuthTokenManager> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns a valid Bearer token, refreshing if expired or about to expire.
    /// </summary>
    public async Task<string> GetTokenAsync(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string? scope = null,
        CancellationToken ct = default)
    {
        // Fast path – token is still valid
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry - TimeSpan.FromSeconds(30))
            return _accessToken;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry - TimeSpan.FromSeconds(30))
                return _accessToken;

            _logger.LogDebug("Acquiring OAuth2 token from {Endpoint}", tokenEndpoint);

            var fields = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            };
            if (!string.IsNullOrWhiteSpace(scope))
                fields["scope"] = scope;

            using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(fields)
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _accessToken = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Token response missing access_token");

            var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            _logger.LogInformation("OAuth2 token acquired; expires in {Seconds}s", expiresIn);

            return _accessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Forces the next call to re-acquire a token.</summary>
    public void Invalidate()
    {
        _accessToken = null;
        _tokenExpiry = DateTimeOffset.MinValue;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _refreshLock.Dispose();
            _disposed = true;
        }
    }
}
