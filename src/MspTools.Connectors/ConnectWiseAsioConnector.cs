using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the ConnectWise Asio (RMM) partner API.
/// Authentication: OAuth2 client_credentials — exchanges Client ID + Client Secret
/// for a Bearer token via POST to oauth2/token before each session.
/// Base URL example: https://openapi.service.itsupport247.net
/// </summary>
public sealed class ConnectWiseAsioConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string Name => "ConnectWise Asio (RMM)";
    public ConnectorType ConnectorType => ConnectorType.ConnectWiseAsio;

    public ConnectWiseAsioConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var response = await _http.GetAsync("v1/companies?pageSize=1", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"Asio API returned {(int)response.StatusCode}. Token may be invalid or lacks permissions.");
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var devices = new List<UnifiedDevice>();
        int page = 0;
        const int pageSize = 500;

        while (true)
        {
            var url = $"v1/devices?pageSize={pageSize}&page={page}";
            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Asio returns { "data": [...], "totalCount": N }
            JsonElement items;
            if (!doc.RootElement.TryGetProperty("data", out items))
                break;

            var list = items.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("name"),
                    AgentName = item.TryGetString("agentName"),
                    CompanyName = item.TryGetString("clientName"),
                    SiteName = item.TryGetString("siteName"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("deviceId"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetString("osName"),
                    IpAddress = item.TryGetString("ipAddress"),
                    MacAddress = item.TryGetString("macAddress"),
                    IsOnline = item.TryGetBool("isOnline"),
                    LastSeenUtc = item.TryGetDateTime("lastSeen"),
                });
            }

            if (list.Count < pageSize) break;
            page++;
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var companies = new List<UnifiedCompany>();
        int page = 0;
        const int pageSize = 500;

        while (true)
        {
            var url = $"v1/companies?pageSize={pageSize}&page={page}";
            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            JsonElement items;
            if (!doc.RootElement.TryGetProperty("data", out items))
                break;

            var list = items.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                var company = new UnifiedCompany
                {
                    CompanyName = item.TryGetString("clientName"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("clientId"),
                    ApiConnectionId = _connection.Id,
                };

                // Collect site names from nested sites array if present
                if (item.TryGetProperty("sites", out var sitesEl))
                    foreach (var s in sitesEl.EnumerateArray())
                    {
                        var sn = s.TryGetString("siteName");
                        if (!string.IsNullOrWhiteSpace(sn))
                            company.SiteNames.Add(sn);
                    }

                companies.Add(company);
            }

            if (list.Count < pageSize) break;
            page++;
        }

        return companies.AsReadOnly();
    }

    private static HttpClient BuildHttpClient(ApiConnection connection)
    {
        var http = new HttpClient { BaseAddress = new Uri(connection.BaseUrl.TrimEnd('/') + '/') };
        // Auth is applied dynamically via EnsureAccessTokenAsync — do not set headers here
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    /// <summary>
    /// Fetches or reuses a valid OAuth2 Bearer token using the client_credentials grant.
    /// Mirrors the proven auth sequence from the reference implementation:
    ///   1. POST JSON to {baseUrl}/v1/token with client_id, client_secret, scope + Basic header.
    ///   2. If invalid_scope, retry without scope.
    ///   Refreshes automatically 60 seconds before expiry.
    /// </summary>
    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return;

        if (_connection.Auth is not ClientCredentialsAuth cca)
            throw new InvalidOperationException(
                "ConnectWise Asio requires Client Credentials authentication (Client ID + Client Secret).");

        var baseUrl = _connection.BaseUrl.TrimEnd('/');
        var tokenEndpoint = $"{baseUrl}/v1/token";

        // Set Basic auth header (client_id:client_secret) for maximum server compatibility
        var basicEncoded = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{cca.ClientId}:{cca.ClientSecret}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicEncoded);

        string? token = null;
        string? lastError = null;

        // Attempt 1: JSON body WITH scope
        if (!string.IsNullOrWhiteSpace(cca.Scope))
        {
            (token, lastError) = await PostTokenJsonAsync(tokenEndpoint, cca, includeScope: true, cancellationToken);
            if (token is not null) { ApplyToken(token, cancellationToken); return; }
        }

        // Attempt 2: JSON body WITHOUT scope (drop scope if invalid or not provided)
        (token, lastError) = await PostTokenJsonAsync(tokenEndpoint, cca, includeScope: false, cancellationToken);
        if (token is not null) { ApplyToken(token, cancellationToken); return; }

        throw new InvalidOperationException(
            $"Asio authentication failed at {tokenEndpoint}. " +
            $"Verify your Client ID and Client Secret. Server: {lastError ?? "no details"}");
    }

    private async Task<(string? token, string? error)> PostTokenJsonAsync(
        string endpoint, ClientCredentialsAuth cca, bool includeScope, CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = cca.ClientId,
            ["client_secret"] = cca.ClientSecret,
        };
        if (includeScope && !string.IsNullOrWhiteSpace(cca.Scope))
            body["scope"] = cca.Scope;

        var json = System.Text.Json.JsonSerializer.Serialize(body);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try { resp = await _http.PostAsync(endpoint, content, ct); }
        catch (Exception ex) { return (null, ex.Message); }

        var responseBody = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            var accessToken = doc.RootElement.TryGetProperty("access_token", out var at)
                ? at.GetString() : null;
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp)
                    ? exp.GetInt32() : 3600;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
                return (accessToken, null);
            }
        }

        // Extract error description from body
        string? errMsg = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
            var desc = doc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : null;
            errMsg = string.Join(" ", new[] { err, desc }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch { errMsg = responseBody[..Math.Min(responseBody.Length, 200)]; }

        return (null, $"HTTP {(int)resp.StatusCode} {errMsg}");
    }

    private void ApplyToken(string token, CancellationToken _)
    {
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void Dispose() => _http.Dispose();
}
