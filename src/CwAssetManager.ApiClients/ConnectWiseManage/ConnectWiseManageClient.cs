using System.Net.Http.Json;
using System.Text.Json;
using CwAssetManager.ApiClients.ConnectWiseManage.Models;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.ApiClients.ConnectWiseManage;

/// <summary>
/// ConnectWise Manage REST API client (OpenAPI 3.0.1, version 2025.16).
/// <para>
/// Uses cursor-based pagination via <c>pageId</c> for efficient traversal of large
/// configuration sets, falling back to <c>page</c>/<c>pageSize</c> on first request.
/// Active configurations are filtered with <c>conditions=activeFlag=true</c>.
/// </para>
/// Authentication is handled by <see cref="ConnectWiseManageAuthHandler"/>.
/// </summary>
public sealed class ConnectWiseManageClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ConnectWiseManageClient> _logger;

    // Fields requested from the API — matches Company.Configuration schema in All.json.
    private const string ConfigFields =
        "id,name,deviceIdentifier,serialNumber,modelNumber,macAddress,ipAddress," +
        "osType,osInfo,mobileGuid,activeFlag,status,type,tagNumber,lastLoginName";

    private const int PageSize = 1000;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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
        int? pageId = null;   // cursor — null means first request
        var page = 1;

        while (true)
        {
            if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Rate limiter timed out waiting for a token.");

            // Use pageId cursor after the first page for efficient server-side pagination.
            var url = pageId.HasValue
                ? $"company/configurations?pageSize={PageSize}&pageId={pageId}&fields={ConfigFields}&conditions=activeFlag=true"
                : $"company/configurations?pageSize={PageSize}&page={page}&fields={ConfigFields}&conditions=activeFlag=true";

            _logger.LogDebug("[Manage] GET {Url}", url);

            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var configs = await resp.Content
                .ReadFromJsonAsync<List<CwManageConfiguration>>(_jsonOptions, ct)
                .ConfigureAwait(false);

            if (configs is null || configs.Count == 0)
                break;

            foreach (var cfg in configs)
                machines.Add(MapToMachine(cfg));

            if (configs.Count < PageSize)
                break;

            // Advance cursor: use last item's id as the pageId for the next request.
            pageId = configs[^1].Id;
            page++;
        }

        _logger.LogInformation("[Manage] Retrieved {Count} active configurations", machines.Count);
        return machines;
    }

    /// <inheritdoc/>
    public async Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Rate limiter timed out.");

        using var resp = await _http.GetAsync(
            $"company/configurations/{providerId}?fields={ConfigFields}", ct)
            .ConfigureAwait(false);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();

        var cfg = await resp.Content
            .ReadFromJsonAsync<CwManageConfiguration>(_jsonOptions, ct)
            .ConfigureAwait(false);

        return cfg is null ? null : MapToMachine(cfg);
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // GET /system/info returns version metadata — lightweight connectivity check.
            using var resp = await _http.GetAsync("system/info", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Manage] Connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Maps a <see cref="CwManageConfiguration"/> (from the Company.Configuration schema)
    /// to the unified <see cref="Machine"/> domain model.
    /// </summary>
    private static Machine MapToMachine(CwManageConfiguration cfg)
    {
        // Prefer osInfo (detailed version string) over osType (short category).
        var os = !string.IsNullOrWhiteSpace(cfg.OsInfo) ? cfg.OsInfo : cfg.OsType;

        // activeFlag=true → Online; false → Offline; absence → Unknown.
        var status = cfg.ActiveFlag ? MachineStatus.Online : MachineStatus.Offline;

        // Status.Name may provide finer-grained state (e.g. "Active", "Inactive", "Retired").
        if (cfg.Status?.Name is { } statusName)
        {
            status = statusName.ToLowerInvariant() switch
            {
                "active" => MachineStatus.Online,
                "inactive" or "retired" or "disposed" => MachineStatus.Offline,
                _ => status
            };
        }

        return new Machine
        {
            // CwManageDeviceId stores the integer record ID as a string.
            CwManageDeviceId = cfg.Id.ToString(),
            // mobileGuid is the BIOS/hardware GUID — the strongest cross-provider key.
            BiosGuid = cfg.MobileGuid,
            Hostname = cfg.Name,
            SerialNumber = cfg.SerialNumber,
            MacAddress = cfg.MacAddress,
            IpAddress = cfg.IpAddress,
            OperatingSystem = os,
            Status = status,
            LastSeen = DateTimeOffset.UtcNow,
        };
    }
}
