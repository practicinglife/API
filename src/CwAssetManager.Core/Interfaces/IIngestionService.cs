using CwAssetManager.Core.Enums;

namespace CwAssetManager.Core.Interfaces;

/// <summary>Orchestrates data ingestion from all CW providers.</summary>
public interface IIngestionService
{
    /// <summary>Runs a full ingestion pass for all enabled providers.</summary>
    Task RunFullIngestionAsync(CancellationToken ct = default);

    /// <summary>Runs ingestion for a single provider.</summary>
    Task RunProviderIngestionAsync(ProviderType provider, CancellationToken ct = default);

    /// <summary>Pauses all ingestion work.</summary>
    void Pause();

    /// <summary>Resumes ingestion after a pause.</summary>
    void Resume();

    /// <summary>Whether ingestion is currently paused.</summary>
    bool IsPaused { get; }
}
