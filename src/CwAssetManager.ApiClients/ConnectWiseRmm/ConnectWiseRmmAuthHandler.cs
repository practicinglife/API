using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Auth;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseRmm;

/// <summary>
/// Delegating handler for ConnectWise RMM (Asio) that injects the API key header.
/// </summary>
public sealed class ConnectWiseRmmAuthHandler : DelegatingHandler
{
    private readonly ApiKeyAuthProvider _authProvider;
    private readonly ILogger<ConnectWiseRmmAuthHandler> _logger;

    public ConnectWiseRmmAuthHandler(ApiKeyAuthProvider authProvider, ILogger<ConnectWiseRmmAuthHandler> logger)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(
            _authProvider.GetApiKeyHeaderName(),
            _authProvider.GetRawApiKey());

        _logger.LogDebug("[RMM] → {Method} {Uri}", request.Method, request.RequestUri);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
