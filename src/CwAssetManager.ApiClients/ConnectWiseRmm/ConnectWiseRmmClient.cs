using System.Text.Json;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseRmm;

/// <summary>
/// ConnectWise RMM (Asio) API client.
/// Retrieves device/endpoint data using the RMM REST API.
/// Authentication via API key header is handled by <see cref="ConnectWiseRmmAuthHandler"/>.
/// </summary>
public sealed class ConnectWiseRmmClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ConnectWiseRmmClient> _logger;
    private const int PageSize = 500;

    public ConnectWiseRmmClient(
        HttpClient http,
        IRateLimiter rateLimiter,
        ILogger<ConnectWiseRmmClient> logger)
    {
        _http = http;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Machine>> GetMachinesAsync(CancellationToken ct = default)
    {
        var machines = new List<Machine>();
        var offset = 0;

        while (true)
        {
            if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Rate limiter timed out.");

            var url = $"devices?limit={PageSize}&offset={offset}";
            _logger.LogDebug("[RMM] GET {Url}", url);

            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement items;
            if (root.ValueKind == JsonValueKind.Array)
                items = root;
            else if (!root.TryGetProperty("data", out items) && !root.TryGetProperty("items", out items))
                break;

            if (items.GetArrayLength() == 0)
                break;

            foreach (var item in items.EnumerateArray())
                machines.Add(MapToMachine(item));

            if (items.GetArrayLength() < PageSize)
                break;

            offset += PageSize;
        }

        _logger.LogInformation("[RMM] Retrieved {Count} devices", machines.Count);
        return machines;
    }

    /// <inheritdoc/>
    public async Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        using var resp = await _http.GetAsync($"devices/{providerId}", ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return MapToMachine(doc.RootElement);
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("account", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RMM] Connection test failed");
            return false;
        }
    }

    private static Machine MapToMachine(JsonElement item)
    {
        var statusStr = GetString(item, "status") ?? GetString(item, "agentStatus");
        return new Machine
        {
            CwRmmDeviceId = GetString(item, "id") ?? GetString(item, "deviceId"),
            Hostname = GetString(item, "hostname") ?? GetString(item, "name"),
            IpAddress = GetString(item, "ipAddress") ?? GetString(item, "ip"),
            MacAddress = GetString(item, "macAddress"),
            SerialNumber = GetString(item, "serialNumber"),
            OperatingSystem = GetString(item, "os") ?? GetString(item, "operatingSystem"),
            Status = statusStr?.ToLowerInvariant() switch
            {
                "online" => MachineStatus.Online,
                "offline" => MachineStatus.Offline,
                _ => MachineStatus.Unknown
            },
            LastSeen = DateTimeOffset.UtcNow
        };
    }

    private static string? GetString(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}
