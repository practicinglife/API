using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the SentinelOne Management Console REST API.
/// Authentication: API Token — supplied via "Authorization: ApiToken &lt;token&gt;" header.
///   Use <see cref="ApiKeyAuth"/> with header name "Authorization" and value "ApiToken &lt;your-token&gt;".
/// Base URL example: https://usea1.sentinelone.net/web/api/v2.1
/// </summary>
public sealed class SentinelOneConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;

    public string Name => "SentinelOne";
    public ConnectorType ConnectorType => ConnectorType.SentinelOne;

    public SentinelOneConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("system/status", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"SentinelOne authentication failed ({(int)response.StatusCode}). " +
                "Verify your API token.");
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<UnifiedDevice>();
        string? cursor = null;
        const int pageSize = 1000;

        while (true)
        {
            var url = $"agents?limit={pageSize}";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                break;

            var list = data.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("computerName"),
                    AgentName = item.TryGetString("agentVersion"),
                    CompanyName = item.TryGetNestedString("site", "name"),
                    SiteName = item.TryGetNestedString("site", "name"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetString("osName"),
                    IpAddress = item.TryGetString("lastIpToMgmt"),
                    MacAddress = item.TryGetString("networkInterfaces") == string.Empty ? null
                        : item.TryGetString("networkInterfaces"),
                    IsOnline = item.TryGetBool("isActive"),
                    LastSeenUtc = item.TryGetDateTime("lastActiveDate"),
                });
            }

            // SentinelOne uses cursor-based pagination
            if (!doc.RootElement.TryGetProperty("pagination", out var pagination)) break;
            var nextCursor = pagination.TryGetString("nextCursor");
            if (string.IsNullOrWhiteSpace(nextCursor)) break;
            cursor = nextCursor;
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = new List<UnifiedCompany>();
        string? cursor = null;
        const int pageSize = 1000;

        while (true)
        {
            var url = $"accounts?limit={pageSize}";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                break;

            var list = data.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                companies.Add(new UnifiedCompany
                {
                    CompanyName = item.TryGetString("name"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                });
            }

            if (!doc.RootElement.TryGetProperty("pagination", out var pagination)) break;
            var nextCursor = pagination.TryGetString("nextCursor");
            if (string.IsNullOrWhiteSpace(nextCursor)) break;
            cursor = nextCursor;
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
