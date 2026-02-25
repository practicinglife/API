using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CwAssetManager.Data.Repositories;

/// <summary>EF Core implementation of IAssetRepository.</summary>
public sealed class MachineRepository : IAssetRepository
{
    private readonly AppDbContext _ctx;

    public MachineRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<Machine?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _ctx.Machines.Include(m => m.Evaluations).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken ct = default)
        => await _ctx.Machines.Include(m => m.Evaluations).ToListAsync(ct);

    public async Task<Machine?> FindByProviderIdAsync(string providerId, CancellationToken ct = default)
        => await _ctx.Machines.FirstOrDefaultAsync(m =>
            m.CwManageDeviceId == providerId ||
            m.CwControlSessionId == providerId ||
            m.CwRmmDeviceId == providerId, ct);

    public async Task<Machine?> FindByHardwareIdAsync(
        string? hostname, string? macAddress, string? serialNumber, string? biosGuid,
        CancellationToken ct = default)
    {
        return await _ctx.Machines.FirstOrDefaultAsync(m =>
            (biosGuid != null && m.BiosGuid == biosGuid) ||
            (serialNumber != null && m.SerialNumber == serialNumber) ||
            (macAddress != null && m.MacAddress == macAddress) ||
            (hostname != null && m.Hostname == hostname), ct);
    }

    public async Task AddAsync(Machine machine, CancellationToken ct = default)
        => await _ctx.Machines.AddAsync(machine, ct);

    public Task UpdateAsync(Machine machine, CancellationToken ct = default)
    {
        _ctx.Machines.Update(machine);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _ctx.Machines.FindAsync(new object[] { id }, ct);
        if (m is not null)
            _ctx.Machines.Remove(m);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);
}
