using CwAssetManager.ApiClients.ConnectWiseControl;
using CwAssetManager.ApiClients.ConnectWiseManage;
using CwAssetManager.ApiClients.ConnectWiseRmm;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.Identity;
using CwAssetManager.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace CwAssetManager.App.Services;

/// <summary>
/// Orchestrates data ingestion from all three CW providers, merges identities,
/// and persists results to the local SQLite database.
/// </summary>
public sealed class IngestionService : IIngestionService
{
    private readonly IAssetRepository _repo;
    private readonly AssetIdentityResolver _resolver;
    private readonly ConnectWiseManageClient _manage;
    private readonly ConnectWiseControlClient _control;
    private readonly ConnectWiseRmmClient _rmm;
    private readonly ILogger<IngestionService> _logger;
    private volatile bool _paused;

    public bool IsPaused => _paused;

    public IngestionService(
        IAssetRepository repo,
        AssetIdentityResolver resolver,
        ConnectWiseManageClient manage,
        ConnectWiseControlClient control,
        ConnectWiseRmmClient rmm,
        ILogger<IngestionService> logger)
    {
        _repo = repo;
        _resolver = resolver;
        _manage = manage;
        _control = control;
        _rmm = rmm;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunFullIngestionAsync(CancellationToken ct = default)
    {
        using var scope = CorrelationIdEnricher.BeginScope();
        _logger.LogInformation("Full ingestion started");

        await RunProviderIngestionAsync(ProviderType.ConnectWiseManage, ct);
        await RunProviderIngestionAsync(ProviderType.ConnectWiseControl, ct);
        await RunProviderIngestionAsync(ProviderType.ConnectWiseRmm, ct);

        _logger.LogInformation("Full ingestion complete");
    }

    /// <inheritdoc/>
    public async Task RunProviderIngestionAsync(ProviderType provider, CancellationToken ct = default)
    {
        if (_paused)
        {
            _logger.LogInformation("Ingestion paused – skipping {Provider}", provider);
            return;
        }

        _logger.LogInformation("Ingesting {Provider}", provider);

        IReadOnlyList<Machine> incoming;
        try
        {
            incoming = provider switch
            {
                ProviderType.ConnectWiseManage  => await _manage.GetMachinesAsync(ct),
                ProviderType.ConnectWiseControl => await _control.GetMachinesAsync(ct),
                ProviderType.ConnectWiseRmm     => await _rmm.GetMachinesAsync(ct),
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch machines from {Provider}", provider);
            return;
        }

        foreach (var inc in incoming)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await UpsertMachineAsync(inc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert machine {Hostname}", inc.Hostname);
            }
        }

        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("{Provider} ingestion complete – {Count} records", provider, incoming.Count);
    }

    /// <inheritdoc/>
    public void Pause()
    {
        _paused = true;
        _logger.LogInformation("Ingestion paused");
    }

    /// <inheritdoc/>
    public void Resume()
    {
        _paused = false;
        _logger.LogInformation("Ingestion resumed");
    }

    private async Task UpsertMachineAsync(Machine incoming, CancellationToken ct)
    {
        var existing = await _repo.FindByHardwareIdAsync(
            incoming.Hostname, incoming.MacAddress,
            incoming.SerialNumber, incoming.BiosGuid, ct);

        if (existing is null && incoming.CwManageDeviceId is not null)
            existing = await _repo.FindByProviderIdAsync(incoming.CwManageDeviceId, ct);
        if (existing is null && incoming.CwControlSessionId is not null)
            existing = await _repo.FindByProviderIdAsync(incoming.CwControlSessionId, ct);
        if (existing is null && incoming.CwRmmDeviceId is not null)
            existing = await _repo.FindByProviderIdAsync(incoming.CwRmmDeviceId, ct);

        if (existing is null)
        {
            await _repo.AddAsync(incoming, ct);
        }
        else
        {
            _resolver.Merge(existing, incoming);
            await _repo.UpdateAsync(existing, ct);
        }
    }
}
