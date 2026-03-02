using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the ConnectWise Control (ScreenConnect) Session Manager API.
/// Authentication: Basic (username + password) or Bearer token.
/// Base URL example: https://your-instance.screenconnect.com/App_Extensions/fc234f0e-2e8e-4a1f-b977-ba41b14031f6
/// </summary>
public sealed class ConnectWiseControlConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;

    public string Name => "ConnectWise Control (ScreenConnect)";
    public ConnectorType ConnectorType => ConnectorType.ConnectWiseControl;

    public ConnectWiseControlConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("GetSessionGroups", cancellationToken);
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

        // Control uses a POST to GetSessions with a filter body
        var payload = new StringContent(
            "{\"SessionType\":2}",   // Type 2 = Access sessions (unattended agents)
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync("GetSessions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        // GetSessions returns an array directly or wrapped in a property
        var sessions = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToList()
            : root.TryGetProperty("Sessions", out var s)
                ? s.EnumerateArray().ToList()
                : new List<JsonElement>();

        foreach (var session in sessions)
        {
            devices.Add(new UnifiedDevice
            {
                ComputerName = session.TryGetString("Name"),
                AgentName = session.TryGetString("Name"),
                CompanyName = session.TryGetNestedString("CustomPropertyValues", 0),
                SiteName = session.TryGetNestedString("CustomPropertyValues", 1),
                SourcePlatform = Name,
                SourceId = session.TryGetString("SessionID"),
                ApiConnectionId = _connection.Id,
                IsOnline = session.TryGetBool("IsConnected"),
                LastSeenUtc = session.TryGetDateTime("LastDisconnected"),
            });
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        // Control does not have a native companies endpoint;
        // derive distinct company names from sessions.
        var devices = await FetchDevicesAsync(cancellationToken);

        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.CompanyName))
            .GroupBy(d => d.CompanyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new UnifiedCompany
            {
                CompanyName = g.Key,
                SourcePlatform = Name,
                SourceId = g.Key,
                ApiConnectionId = _connection.Id,
                SiteNames = g.Where(d => !string.IsNullOrWhiteSpace(d.SiteName))
                              .Select(d => d.SiteName)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList(),
            })
            .ToList()
            .AsReadOnly();
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
