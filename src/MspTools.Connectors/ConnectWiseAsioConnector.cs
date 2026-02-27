using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the ConnectWise Asio (RMM) partner API.
/// Authentication: API Key via x-api-key header (ApiKeyAuth).
/// Base URL example: https://openapi.service.itsupport247.net
/// </summary>
public sealed class ConnectWiseAsioConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;

    public string Name => "ConnectWise Asio (RMM)";
    public ConnectorType ConnectorType => ConnectorType.ConnectWiseAsio;

    public ConnectWiseAsioConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("v1/companies?pageSize=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
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
        foreach (var header in connection.Auth.GetHeaders())
            http.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    public void Dispose() => _http.Dispose();
}
