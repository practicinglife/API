using CwAssetManager.Core.Models;

namespace CwAssetManager.Core.Interfaces;

/// <summary>CRUD repository for Machine assets.</summary>
public interface IAssetRepository
{
    Task<Machine?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken ct = default);
    Task<Machine?> FindByProviderIdAsync(string providerId, CancellationToken ct = default);
    Task<Machine?> FindByHardwareIdAsync(string? hostname, string? macAddress, string? serialNumber, string? biosGuid, CancellationToken ct = default);
    Task AddAsync(Machine machine, CancellationToken ct = default);
    Task UpdateAsync(Machine machine, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
