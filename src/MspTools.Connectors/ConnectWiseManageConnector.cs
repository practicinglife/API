using System.Net.Http.Headers;
using System.Text.Json;
using MspTools.Core.Authentication;
using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Connectors;

/// <summary>
/// Connects to the ConnectWise Manage (PSA) REST API.
/// Authentication: CompanyId + PublicKey + PrivateKey (ConnectWiseApiKeyAuth) with a clientId header.
/// Base URL example: https://na.myconnectwise.net/v4_6_release/apis/3.0
/// </summary>
public sealed class ConnectWiseManageConnector : IApiConnector, IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _http;

    public string Name => "ConnectWise Manage";
    public ConnectorType ConnectorType => ConnectorType.ConnectWiseManage;

    public ConnectWiseManageConnector(ApiConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = BuildHttpClient(connection);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("system/info", cancellationToken);
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
        int page = 1;
        const int pageSize = 1000;

        while (true)
        {
            var url = $"company/configurations?pageSize={pageSize}&page={page}&fields=id,name,company,site,ipAddress,macAddress,serialNumber,osType,status,lastModified";
            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray().ToList();
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                devices.Add(new UnifiedDevice
                {
                    ComputerName = item.TryGetString("name"),
                    CompanyName = item.TryGetNestedString("company", "name"),
                    SiteName = item.TryGetNestedString("site", "name"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                    OperatingSystem = item.TryGetString("osType"),
                    IpAddress = item.TryGetString("ipAddress"),
                    MacAddress = item.TryGetString("macAddress"),
                    SerialNumber = item.TryGetString("serialNumber"),
                });
            }

            if (items.Count < pageSize) break;
            page++;
        }

        return devices.AsReadOnly();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> FetchCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = new List<UnifiedCompany>();
        int page = 1;
        const int pageSize = 1000;

        while (true)
        {
            var url = $"company/companies?pageSize={pageSize}&page={page}&fields=id,identifier,name,phoneNumber,website,addressLine1,city,state,zip,country";
            var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray().ToList();
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                companies.Add(new UnifiedCompany
                {
                    CompanyName = item.TryGetString("name"),
                    CompanyIdentifier = item.TryGetString("identifier"),
                    SourcePlatform = Name,
                    SourceId = item.TryGetString("id"),
                    ApiConnectionId = _connection.Id,
                    PhoneNumber = item.TryGetString("phoneNumber"),
                    Website = item.TryGetString("website"),
                    City = item.TryGetString("city"),
                    State = item.TryGetNestedString("state", "name"),
                    Country = item.TryGetNestedString("country", "name"),
                });
            }

            if (items.Count < pageSize) break;
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
