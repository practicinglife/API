using Microsoft.EntityFrameworkCore;

namespace MspTools.App.Data;

public sealed class MspToolsDbContext : DbContext
{
    private readonly string _connectionString;

    public MspToolsDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbSet<DbDevice> Devices => Set<DbDevice>();
    public DbSet<DbCompany> Companies => Set<DbCompany>();
    public DbSet<DbMatch> Matches => Set<DbMatch>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DbDevice>()
            .HasIndex(d => new { d.SourcePlatform, d.SourceId })
            .HasDatabaseName("IX_devices_source");

        model.Entity<DbDevice>()
            .HasIndex(d => d.CompanyName)
            .HasDatabaseName("IX_devices_company");

        model.Entity<DbCompany>()
            .HasIndex(c => new { c.SourcePlatform, c.SourceId })
            .HasDatabaseName("IX_companies_source");

        model.Entity<DbMatch>()
            .HasIndex(m => m.MatchKey)
            .HasDatabaseName("IX_matches_key");
    }
}
