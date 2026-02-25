using System.Net.Http.Headers;
using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Auth;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseControl;

/// <summary>
/// Delegating handler for ConnectWise Control that authenticates each request using either:
/// <list type="bullet">
///   <item>
///     <term>API Key</term>
///     <description>
///       If <see cref="AuthConfig.CwControlApiKey"/> is set, injects
///       <c>Authorization: Bearer {apiKey}</c> directly — no token exchange required.
///       This is the recommended approach for server-to-server access as documented in
///       the Session Manager API reference.
///     </description>
///   </item>
///   <item>
///     <term>OAuth2 Client Credentials</term>
///     <description>
///       Falls back to obtaining a Bearer token via the OAuth2 client credentials flow
///       when no API key is configured. Tokens are cached and refreshed 30 s before expiry.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class ConnectWiseControlAuthHandler : DelegatingHandler
{
    private readonly OAuthTokenManager _tokenManager;
    private readonly AuthConfig _config;
    private readonly ILogger<ConnectWiseControlAuthHandler> _logger;

    public ConnectWiseControlAuthHandler(
        OAuthTokenManager tokenManager,
        AuthConfig config,
        ILogger<ConnectWiseControlAuthHandler> logger)
    {
        _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_config.CwControlApiKey))
        {
            // Prefer direct API key — avoids an extra round-trip to the token endpoint.
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.CwControlApiKey);
        }
        else
        {
            // OAuth2 client credentials flow — fetches (and caches) a token.
            var token = await _tokenManager.GetTokenAsync(
                _config.OAuthTokenEndpoint
                    ?? throw new InvalidOperationException("OAuthTokenEndpoint is not configured"),
                _config.OAuthClientId
                    ?? throw new InvalidOperationException("OAuthClientId is not configured"),
                _config.OAuthClientSecret
                    ?? throw new InvalidOperationException("OAuthClientSecret is not configured"),
                _config.OAuthScope,
                cancellationToken).ConfigureAwait(false);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        _logger.LogDebug("[Control] → {Method} {Uri}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Invalidate cached OAuth token on 401 so the next request triggers a fresh exchange.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && string.IsNullOrWhiteSpace(_config.CwControlApiKey))
        {
            _tokenManager.Invalidate();
            _logger.LogWarning("[Control] 401 received — OAuth token invalidated; will refresh on next request");
        }

        return response;
    }
}
