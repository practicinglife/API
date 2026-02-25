using System.Net.Http.Headers;
using System.Text;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseManage;

/// <summary>
/// Delegating handler that adds CW Manage authentication headers to every outbound request:
///   - Authorization: Basic {companyId+publicKey:privateKey}
///   - clientId: {clientId}
/// </summary>
public sealed class ConnectWiseManageAuthHandler : DelegatingHandler
{
    private readonly AuthConfig _config;
    private readonly ILogger<ConnectWiseManageAuthHandler> _logger;

    public ConnectWiseManageAuthHandler(AuthConfig config, ILogger<ConnectWiseManageAuthHandler> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Build Basic credentials: Base64(companyId+publicKey:privateKey)
        var credentials = $"{_config.CompanyId}+{_config.ApiPublicKey}:{_config.ApiPrivateKey}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        if (!string.IsNullOrWhiteSpace(_config.ApiClientId))
            request.Headers.TryAddWithoutValidation("clientId", _config.ApiClientId);

        _logger.LogDebug("[Manage] → {Method} {Uri}", request.Method, request.RequestUri);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
