using CwAssetManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CwAssetManager.Data;

/// <summary>EF Core DbContext for the CW Asset Manager application.</summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<AssetEvaluation> AssetEvaluations => Set<AssetEvaluation>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<ProviderConfig> ProviderConfigs => Set<ProviderConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Machine>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.Hostname).HasMaxLength(255);
            e.Property(m => m.MacAddress).HasMaxLength(64);
            e.Property(m => m.SerialNumber).HasMaxLength(128);
            e.Property(m => m.BiosGuid).HasMaxLength(64);
            e.Property(m => m.CwManageDeviceId).HasMaxLength(64);
            e.Property(m => m.CwManageDeviceIdentifier).HasMaxLength(128);
            e.Property(m => m.CwControlSessionId).HasMaxLength(128);
            e.Property(m => m.CwRmmDeviceId).HasMaxLength(128);
            e.Property(m => m.OperatingSystem).HasMaxLength(256);
            e.Property(m => m.IpAddress).HasMaxLength(64);
            e.HasIndex(m => m.Hostname);
            e.HasIndex(m => m.MacAddress);
            e.HasIndex(m => m.SerialNumber);
            e.HasMany(m => m.Evaluations)
             .WithOne(ev => ev.Machine)
             .HasForeignKey(ev => ev.MachineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetEvaluation>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<RequestLog>(e =>
        {
            e.HasKey(rl => rl.Id);
            e.Property(rl => rl.Id).ValueGeneratedNever();
            e.Property(rl => rl.Method).HasMaxLength(16);
            e.Property(rl => rl.Url).HasMaxLength(2048);
            e.Property(rl => rl.CorrelationId).HasMaxLength(64);
            e.HasIndex(rl => rl.Timestamp);
            e.HasIndex(rl => rl.Provider);
        });

        modelBuilder.Entity<ProviderConfig>(e =>
        {
            e.HasKey(pc => pc.Id);
            e.Property(pc => pc.Id).ValueGeneratedNever();
            e.Property(pc => pc.BaseUrl).HasMaxLength(512);
        });
    }
}

/// <summary>Records each API request/response for audit and debugging.</summary>
public sealed class RequestLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Provider { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
