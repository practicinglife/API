using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the Acronis Cyber Cloud REST API.
/// Authentication: OAuth2 client_credentials — exchanges Client ID + Client Secret
/// for a Bearer token via POST to {baseUrl}/idp/token.
/// Base URL example: https://cloud.acronis.com
/// </summary>
public sealed class AcronisConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string Name => "Acronis";
    public ConnectorType ConnectorType => ConnectorType.Acronis;

    public AcronisConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var response = await _http.GetAsync("api/2/tenants?limit=1", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"Acronis authentication failed ({(int)response.StatusCode}). " +
                "Verify your Client ID and Client Secret.");
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var devices = new List<UnifiedDevice>();
        string? afterId = null;
        const int pageSize = 500;

        // Resolve the root tenant id first
        var rootTenantId = await GetRootTenantIdAsync(cancellationToken);

        while (true)
        {
            var url = $"api/2/resources?limit={pageSize}&tenant={rootTenantId}&of_any_type=true";
            if (afterId is not null) url += $"&after_id={afterId}";

            var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items))
                break;

            var list = items.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("name"),
                    AgentName = item.TryGetString("name"),
                    CompanyName = item.TryGetNestedString("tenant", "name"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetNestedString("context", "platform"),
                    IsOnline = item.TryGetBool("online"),
                    LastSeenUtc = item.TryGetDateTime("last_seen"),
                });
            }

            if (list.Count < pageSize) break;
            afterId = list.Last().TryGetString("id");
            if (string.IsNullOrWhiteSpace(afterId)) break;
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var companies = new List<UnifiedCompany>();

        var rootTenantId = await GetRootTenantIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rootTenantId))
            return companies.AsReadOnly();

        // Fetch child tenants of the root (these represent customer companies)
        var response = await _http.GetAsync(
            $"api/2/tenants?parent_id={rootTenantId}&limit=500", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return companies.AsReadOnly();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("items", out var items))
            return companies.AsReadOnly();

        foreach (var item in items.EnumerateArray())
        {
            companies.Add(new UnifiedCompany
            {
                CompanyName = item.TryGetString("name"),
                SourcePlatform = Name,
                SourceId = item.TryGetString("id"),
                ApiConnectionId = _connection.Id,
            });
        }

        return companies.AsReadOnly();
    }

    private async Task<string> GetRootTenantIdAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("api/2/tenants/me", cancellationToken);
        if (!response.IsSuccessStatusCode) return string.Empty;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetString("id");
    }

    private static HttpClient BuildHttpClient(ApiConnection connection)
    {
        var http = new HttpClient { BaseAddress = new Uri(connection.BaseUrl.TrimEnd('/') + '/') };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    /// <summary>
    /// Fetches or reuses a valid OAuth2 Bearer token using the client_credentials grant.
    /// Refreshes automatically 60 seconds before expiry.
    /// </summary>
    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return;

        if (_connection.Auth is not ClientCredentialsAuth cca)
            throw new InvalidOperationException(
                "Acronis requires Client Credentials authentication (Client ID + Client Secret).");

        var baseUrl = _connection.BaseUrl.TrimEnd('/');
        var tokenEndpoint = $"{baseUrl}/idp/token";

        // Use a dedicated client so shared _http state is not mutated during token acquisition
        var basicEncoded = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{cca.ClientId}:{cca.ClientSecret}"));

        using var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        });

        using var tokenClient = new HttpClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicEncoded);

        HttpResponseMessage resp;
        try { resp = await tokenClient.PostAsync(tokenEndpoint, tokenContent, cancellationToken); }
        catch (Exception ex) { throw new InvalidOperationException($"Acronis token request failed: {ex.Message}", ex); }

        var responseBody = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Acronis authentication failed at {tokenEndpoint}: HTTP {(int)resp.StatusCode} {responseBody[..Math.Min(responseBody.Length, 200)]}");

        using var doc = JsonDocument.Parse(responseBody);
        var token = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Acronis token response did not contain access_token.");

        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void Dispose() => _http.Dispose();
}
