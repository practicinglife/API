using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the Microsoft Graph API to manage Office 365 / Entra ID devices and tenants.
/// Authentication: OAuth2 client_credentials — exchanges Tenant ID + Client ID + Client Secret
///   for a Bearer token via POST to https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token.
///   Store <c>TenantId</c> in <see cref="ClientCredentialsAuth.Scope"/> or prefix the Client ID as
///   <c>tenantId|clientId</c> separated by '|'.
/// Base URL: https://graph.microsoft.com/v1.0
/// </summary>
public sealed class Office365Connector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string Name => "Office 365";
    public ConnectorType ConnectorType => ConnectorType.Office365;

    public Office365Connector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var response = await _http.GetAsync("organization?$select=displayName&$top=1", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"Office 365 authentication failed ({(int)response.StatusCode}). " +
                "Verify your Tenant ID, Client ID, and Client Secret.");
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var devices = new List<UnifiedDevice>();
        string? nextLink = "deviceManagement/managedDevices?$top=999";

        while (nextLink is not null)
        {
            var response = await _http.GetAsync(nextLink, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value))
                break;

            foreach (var item in value.EnumerateArray())
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("deviceName"),
                    AgentName = item.TryGetString("managedDeviceName"),
                    CompanyName = item.TryGetString("userDisplayName"),
                    SiteName = string.Empty,
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetString("operatingSystem"),
                    SerialNumber = item.TryGetString("serialNumber"),
                    IsOnline = null,
                    LastSeenUtc = item.TryGetDateTime("lastSyncDateTime"),
                });
            }

            nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? nl.GetString()
                : null;
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var companies = new List<UnifiedCompany>();

        var response = await _http.GetAsync(
            "organization?$select=id,displayName,city,state,country,businessPhones,verifiedDomains",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return companies.AsReadOnly();

        foreach (var item in value.EnumerateArray())
        {
            var company = new UnifiedCompany
            {
                CompanyName = item.TryGetString("displayName"),
                SourcePlatform = Name,
                SourceId = item.TryGetString("id"),
                ApiConnectionId = _connection.Id,
                City = item.TryGetString("city"),
                State = item.TryGetString("state"),
                Country = item.TryGetString("country"),
            };

            // Collect verified domain names as site identifiers
            if (item.TryGetProperty("verifiedDomains", out var domains))
                foreach (var d in domains.EnumerateArray())
                {
                    var domainName = d.TryGetString("name");
                    if (!string.IsNullOrWhiteSpace(domainName))
                        company.SiteNames.Add(domainName);
                }

            // Business phone
            if (item.TryGetProperty("businessPhones", out var phones))
                foreach (var p in phones.EnumerateArray())
                {
                    var phone = p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(phone))
                    { company.PhoneNumber = phone; break; }
                }

            companies.Add(company);
        }

        return companies.AsReadOnly();
    }

    private static HttpClient BuildHttpClient(ApiConnection connection)
    {
        var http = new HttpClient { BaseAddress = new Uri(connection.BaseUrl.TrimEnd('/') + '/') };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    /// <summary>
    /// Acquires an OAuth2 Bearer token from Azure AD using the client_credentials grant.
    /// <para>
    /// The Tenant ID must be provided in one of two ways:
    /// <list type="bullet">
    ///   <item>Set <see cref="ClientCredentialsAuth.Scope"/> to <c>tenantId|https://graph.microsoft.com/.default</c></item>
    ///   <item>Set <see cref="ClientCredentialsAuth.ClientId"/> to <c>tenantId|clientId</c></item>
    /// </list>
    /// The separator used is the pipe character '|'.
    /// </para>
    /// </summary>
    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return;

        if (_connection.Auth is not ClientCredentialsAuth cca)
            throw new InvalidOperationException(
                "Office 365 requires Client Credentials authentication (Tenant ID, Client ID, Client Secret). " +
                "Store the Tenant ID as the first part of the Client ID field separated by '|' (e.g. tenantId|clientId).");

        // Parse tenantId|clientId from ClientId field
        var clientIdField = cca.ClientId ?? string.Empty;
        string tenantId;
        string clientId;

        if (clientIdField.Contains('|'))
        {
            var parts = clientIdField.Split('|', 2);
            tenantId = parts[0].Trim();
            clientId = parts[1].Trim();
        }
        else
        {
            // Fall back: expect scope to contain the tenant id prefix
            var scopeParts = (cca.Scope ?? string.Empty).Split('|', 2);
            tenantId = scopeParts.Length > 1 ? scopeParts[0].Trim() : clientIdField;
            clientId = clientIdField;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException(
                "Office 365 connector: Tenant ID is required. " +
                "Provide it as 'tenantId|clientId' in the Client ID field.");

        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        var scope = cca.Scope?.Contains('|') == true
            ? cca.Scope.Split('|', 2)[1].Trim()
            : (string.IsNullOrWhiteSpace(cca.Scope) ? "https://graph.microsoft.com/.default" : cca.Scope);

        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", cca.ClientSecret),
            new KeyValuePair<string, string>("scope", scope),
        });

        using var tokenClient = new HttpClient();
        HttpResponseMessage resp;
        try { resp = await tokenClient.PostAsync(tokenEndpoint, content, cancellationToken); }
        catch (Exception ex) { throw new InvalidOperationException($"Office 365 token request failed: {ex.Message}", ex); }

        var responseBody = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Office 365 authentication failed at {tokenEndpoint}: " +
                $"HTTP {(int)resp.StatusCode} {responseBody[..Math.Min(responseBody.Length, 200)]}");

        using var doc = JsonDocument.Parse(responseBody);
        var token = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Office 365 token response did not contain access_token.");

        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void Dispose() => _http.Dispose();
}
