using CwAssetManager.Core.Models;

namespace CwAssetManager.Core.Interfaces;

/// <summary>Generic API client for a ConnectWise provider.</summary>
public interface IApiClient
{
    /// <summary>Fetches all machines/devices from the provider.</summary>
    Task<IReadOnlyList<Machine>> GetMachinesAsync(CancellationToken ct = default);

    /// <summary>Fetches a single machine by its provider-specific ID.</summary>
    Task<Machine?> GetMachineByIdAsync(string providerId, CancellationToken ct = default);

    /// <summary>Tests connectivity and authentication to the provider.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
