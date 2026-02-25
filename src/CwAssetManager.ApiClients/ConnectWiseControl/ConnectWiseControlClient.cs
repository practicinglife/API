using System.Text.Json;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseControl;

/// <summary>
/// ConnectWise Control (ScreenConnect) API client.
/// Retrieves session/device data using the Control REST API.
/// Authentication is handled by <see cref="ConnectWiseControlAuthHandler"/>.
/// </summary>
public sealed class ConnectWiseControlClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ConnectWiseControlClient> _logger;

    public ConnectWiseControlClient(
        HttpClient http,
        IRateLimiter rateLimiter,
        ILogger<ConnectWiseControlClient> logger)
    {
        _http = http;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Machine>> GetMachinesAsync(CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        _logger.LogDebug("[Control] GET Sessions");
        using var resp = await _http.GetAsync("Session", ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var machines = new List<Machine>();
        var root = doc.RootElement;

        // CW Control returns { "SessionCount": N, "Sessions": [...] }
        var sessions = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("Sessions", out var s) ? s : root;

        foreach (var session in sessions.EnumerateArray())
            machines.Add(MapToMachine(session));

        _logger.LogInformation("[Control] Retrieved {Count} sessions", machines.Count);
        return machines;
    }

    /// <inheritdoc/>
    public async Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        using var resp = await _http.GetAsync($"Session/{providerId}", ct).ConfigureAwait(false);
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
            using var resp = await _http.GetAsync("Status", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Control] Connection test failed");
            return false;
        }
    }

    private static Machine MapToMachine(JsonElement item)
    {
        var sessionId = GetString(item, "SessionID") ?? GetString(item, "SessionId");
        var name = GetString(item, "Name") ?? GetString(item, "MachineName");
        var isOnline = GetBool(item, "IsOnline");

        return new Machine
        {
            CwControlSessionId = sessionId,
            Hostname = name,
            IpAddress = GetString(item, "GuestLastActivityTime") is not null
                ? GetString(item, "GuestIpAddress")
                : null,
            OperatingSystem = GetString(item, "GuestOperatingSystemName"),
            Status = isOnline ? MachineStatus.Online : MachineStatus.Offline,
            LastSeen = DateTimeOffset.UtcNow
        };
    }

    private static string? GetString(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static bool GetBool(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var prop)) return false;
        return prop.ValueKind == JsonValueKind.True;
    }
}
