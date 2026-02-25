using System.Net.Http.Json;
using System.Text.Json;
using CwAssetManager.ApiClients.ConnectWiseControl.Models;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseControl;

/// <summary>
/// ConnectWise Control (ScreenConnect) Session Manager API client.
/// <para>
/// Uses the JSON-RPC endpoint at <c>POST {serverUrl}/Services/PageService.ashx</c>
/// as documented in the Session Manager API reference. Only <c>SessionType=Access</c>
/// sessions are returned — these represent persistently managed endpoints (agents).
/// </para>
/// Authentication is handled by <see cref="ConnectWiseControlAuthHandler"/>.
/// </summary>
public sealed class ConnectWiseControlClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ConnectWiseControlClient> _logger;

    /// <summary>JSON-RPC endpoint for all Session Manager calls.</summary>
    private const string RpcEndpoint = "Services/PageService.ashx";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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

        _logger.LogDebug("[Control] RPC GetSessions (SessionType=Access)");

        // Call GetSessionsAsync with a filter for Access sessions (managed endpoints only).
        // sessionFilter type: SessionType enumeration value 2 = Access.
        var rpcRequest = new CwControlRpcRequest
        {
            Id = 1,
            Method = "GetSessions",
            Params = new { sessionFilter = new { SessionType = 2 } }
        };

        using var resp = await _http
            .PostAsJsonAsync(RpcEndpoint, rpcRequest, _jsonOptions, ct)
            .ConfigureAwait(false);

        resp.EnsureSuccessStatusCode();

        var rpcResp = await resp.Content
            .ReadFromJsonAsync<CwControlRpcResponse<List<CwControlSession>>>(_jsonOptions, ct)
            .ConfigureAwait(false);

        if (rpcResp?.Error is { } err)
            throw new InvalidOperationException($"[Control] RPC error {err.Code}: {err.Message}");

        var sessions = rpcResp?.Result ?? [];
        var machines = sessions.Select(MapToMachine).ToList();

        _logger.LogInformation("[Control] Retrieved {Count} Access sessions", machines.Count);
        return machines;
    }

    /// <inheritdoc/>
    public async Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        if (!Guid.TryParse(providerId, out var sessionGuid))
        {
            _logger.LogWarning("[Control] Invalid session GUID: {Id}", providerId);
            return null;
        }

        var rpcRequest = new CwControlRpcRequest
        {
            Id = 1,
            Method = "GetSession",
            Params = new { sessionID = sessionGuid }
        };

        using var resp = await _http
            .PostAsJsonAsync(RpcEndpoint, rpcRequest, _jsonOptions, ct)
            .ConfigureAwait(false);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();

        var rpcResp = await resp.Content
            .ReadFromJsonAsync<CwControlRpcResponse<CwControlSession>>(_jsonOptions, ct)
            .ConfigureAwait(false);

        if (rpcResp?.Error is { } err)
            throw new InvalidOperationException($"[Control] RPC error {err.Code}: {err.Message}");

        return rpcResp?.Result is { } session ? MapToMachine(session) : null;
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // GetSessions() with no filter — returns minimal data, just confirms auth.
            var rpcRequest = new CwControlRpcRequest
            {
                Id = 1,
                Method = "GetSessions",
                Params = new object[] { }
            };

            using var resp = await _http
                .PostAsJsonAsync(RpcEndpoint, rpcRequest, _jsonOptions, ct)
                .ConfigureAwait(false);

            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Control] Connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Maps a <see cref="CwControlSession"/> to the unified <see cref="Machine"/> domain model.
    /// Guest information (OS, IP, serial) comes from the agent-populated <c>GuestInfo</c> object.
    /// </summary>
    private static Machine MapToMachine(CwControlSession session)
    {
        var guestInfo = session.GuestInfo;

        // Prefer the agent-reported private IP from GuestInfo; fall back to null.
        var ip = guestInfo?.GuestPrivateIpAddress;

        // Combine OS name and version for a richer string (e.g. "Windows 11 Pro 22H2").
        string? os = null;
        if (!string.IsNullOrWhiteSpace(guestInfo?.OperatingSystemName))
        {
            os = string.IsNullOrWhiteSpace(guestInfo.OperatingSystemVersion)
                ? guestInfo.OperatingSystemName
                : $"{guestInfo.OperatingSystemName} {guestInfo.OperatingSystemVersion}";
        }

        // A session is "online" when at least one Guest-type connection is active.
        var status = session.IsOnline ? MachineStatus.Online : MachineStatus.Offline;

        return new Machine
        {
            CwControlSessionId = session.SessionId.ToString(),
            Hostname = session.Name,
            IpAddress = ip,
            SerialNumber = guestInfo?.GuestMachineSerialNumber,
            OperatingSystem = os,
            Status = status,
            // GuestInfoUpdateTime provides the most accurate "last seen" timestamp.
            LastSeen = session.GuestInfoUpdateTime ?? DateTimeOffset.UtcNow,
        };
    }
}
