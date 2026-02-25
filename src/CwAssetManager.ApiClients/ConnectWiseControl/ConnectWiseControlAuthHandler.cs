using System.Net.Http.Headers;
using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Auth;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseControl;

/// <summary>
/// Delegating handler for ConnectWise Control that injects a Bearer token
/// obtained via OAuth2 client credentials.
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
        var token = await _tokenManager.GetTokenAsync(
            _config.OAuthTokenEndpoint ?? throw new InvalidOperationException("OAuthTokenEndpoint is not configured"),
            _config.OAuthClientId ?? throw new InvalidOperationException("OAuthClientId is not configured"),
            _config.OAuthClientSecret ?? throw new InvalidOperationException("OAuthClientSecret is not configured"),
            _config.OAuthScope,
            cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _logger.LogDebug("[Control] → {Method} {Uri}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Token may have been invalidated on 401
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _tokenManager.Invalidate();
            _logger.LogWarning("[Control] 401 received – token invalidated");
        }

        return response;
    }
}
