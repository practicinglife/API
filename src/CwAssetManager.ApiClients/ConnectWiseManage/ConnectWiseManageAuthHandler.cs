using System.Net.Http.Headers;
using System.Text;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseManage;

/// <summary>
/// Delegating handler that adds ConnectWise Manage authentication and versioning headers
/// to every outbound request.
/// <para>
/// Auth format (per the OpenAPI 3.0.1 spec, version 2025.16):
/// <code>Authorization: Basic Base64("{companyId}+{publicKey}:{privateKey}")</code>
/// <code>clientId: {clientId}  (registered at developer.connectwise.com)</code>
/// </para>
/// The <c>Accept</c> and <c>Content-Type</c> headers are set to the versioned media type
/// <c>application/vnd.connectwise.com+json; version=2025.16</c> so the server returns
/// the canonical schema used in the spec.
/// </summary>
public sealed class ConnectWiseManageAuthHandler : DelegatingHandler
{
    private const string MediaType = "application/vnd.connectwise.com+json; version=2025.16";

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
        // Build Basic credentials: Base64("{companyId}+{publicKey}:{privateKey}")
        var credentials = $"{_config.CompanyId}+{_config.ApiPublicKey}:{_config.ApiPrivateKey}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        // clientId header is required for all CW Manage API calls.
        if (!string.IsNullOrWhiteSpace(_config.ApiClientId))
            request.Headers.TryAddWithoutValidation("clientId", _config.ApiClientId);

        // Versioned Accept header — requests the canonical schema from the 2025.16 spec.
        request.Headers.TryAddWithoutValidation("Accept", MediaType);

        _logger.LogDebug("[Manage] → {Method} {Uri}", request.Method, request.RequestUri);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
