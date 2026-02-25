using Microsoft.EntityFrameworkCore;

namespace CwAssetManager.Data.Repositories;

/// <summary>Repository for reading/writing request/response audit logs.</summary>
public sealed class RequestLogRepository
{
    private readonly AppDbContext _ctx;

    public RequestLogRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task AddAsync(RequestLog log, CancellationToken ct = default)
        => await _ctx.RequestLogs.AddAsync(log, ct);

    public async Task<IReadOnlyList<RequestLog>> GetRecentAsync(int count = 100, CancellationToken ct = default)
        => await _ctx.RequestLogs
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RequestLog>> GetByProviderAsync(string provider, int count = 100, CancellationToken ct = default)
        => await _ctx.RequestLogs
            .Where(r => r.Provider == provider)
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RequestLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default)
        => await _ctx.RequestLogs
            .Where(r => r.CorrelationId == correlationId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(ct);

    public async Task PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var old = await _ctx.RequestLogs.Where(r => r.Timestamp < cutoff).ToListAsync(ct);
        _ctx.RequestLogs.RemoveRange(old);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);
}
