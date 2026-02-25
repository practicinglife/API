using System.Net.Http.Headers;
using System.Text.Json;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseManage;

/// <summary>
/// ConnectWise Manage REST API client.
/// Supports paginated /company/configurations and /service/tickets endpoints.
/// Authentication is handled by <see cref="ConnectWiseManageAuthHandler"/>.
/// </summary>
public sealed class ConnectWiseManageClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ConnectWiseManageClient> _logger;
    private const int PageSize = 1000;

    public ConnectWiseManageClient(
        HttpClient http,
        IRateLimiter rateLimiter,
        ILogger<ConnectWiseManageClient> logger)
    {
        _http = http;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Machine>> GetMachinesAsync(CancellationToken ct = default)
    {
        var machines = new List<Machine>();
        var page = 1;

        while (true)
        {
            if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Rate limiter timed out waiting for a token.");

            var url = $"company/configurations?pageSize={PageSize}&page={page}&fields=id,name,deviceIdentifier,ipAddress,osType,lastUpdated,status";
            _logger.LogDebug("[Manage] GET {Url}", url);

            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement;

            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
                break;

            foreach (var item in items.EnumerateArray())
            {
                var machine = MapToMachine(item);
                machines.Add(machine);
            }

            if (items.GetArrayLength() < PageSize)
                break;

            page++;
        }

        _logger.LogInformation("[Manage] Retrieved {Count} configurations", machines.Count);
        return machines;
    }

    /// <inheritdoc/>
    public async Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        using var resp = await _http.GetAsync($"company/configurations/{providerId}", ct).ConfigureAwait(false);
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
            using var resp = await _http.GetAsync("system/info", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Manage] Connection test failed");
            return false;
        }
    }

    private static Machine MapToMachine(JsonElement item)
    {
        var machine = new Machine
        {
            CwManageDeviceId = GetString(item, "id"),
            Hostname = GetString(item, "name"),
            IpAddress = GetString(item, "ipAddress"),
            OperatingSystem = GetString(item, "osType"),
            LastSeen = DateTimeOffset.UtcNow,
            Status = MachineStatus.Unknown
        };

        var statusStr = GetString(item, "status.name");
        machine.Status = statusStr?.ToLowerInvariant() switch
        {
            "active" => MachineStatus.Online,
            "inactive" => MachineStatus.Offline,
            _ => MachineStatus.Unknown
        };

        return machine;
    }

    private static string? GetString(JsonElement element, string path)
    {
        var parts = path.Split('.');
        JsonElement current = element;
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
