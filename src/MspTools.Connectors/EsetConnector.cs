using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the ESET PROTECT API.
/// Authentication: Basic auth (username + password).
/// Base URL example: https://your-eset-server/era/v1
/// </summary>
public sealed class EsetConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;

    public string Name => "ESET PROTECT";
    public ConnectorType ConnectorType => ConnectorType.Eset;

    public EsetConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("computers?pageSize=1", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"ESET authentication failed ({(int)response.StatusCode}). " +
                "Verify your ESET PROTECT username and password.");
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<UnifiedDevice>> FetchDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<UnifiedDevice>();
        int page = 0;
        const int pageSize = 500;

        while (true)
        {
            var url = $"computers?pageSize={pageSize}&pageNumber={page}";
            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            JsonElement items;
            if (!doc.RootElement.TryGetProperty("computers", out items))
                break;

            var list = items.EnumerateArray().ToList();
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("computerName"),
                    AgentName = item.TryGetString("agentVersion"),
                    CompanyName = item.TryGetString("groupName"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("computerId"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetString("osName"),
                    IpAddress = item.TryGetString("ipAddress"),
                    IsOnline = item.TryGetBool("isManaged"),
                    LastSeenUtc = item.TryGetDateTime("lastConnectedTime"),
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

        var response = await _http.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        JsonElement items;
        if (!doc.RootElement.TryGetProperty("groups", out items))
            return companies.AsReadOnly();

        foreach (var item in items.EnumerateArray())
        {
            companies.Add(new UnifiedCompany
            {
                CompanyName = item.TryGetString("groupName"),
                SourcePlatform = Name,
                SourceId = item.TryGetString("groupId"),
                ApiConnectionId = _connection.Id,
            });
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
