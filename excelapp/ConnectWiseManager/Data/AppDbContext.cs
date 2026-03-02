using ConnectWiseManager.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Data.Common;

namespace ConnectWiseManager.Data;

public class AppDbContext : DbContext
{
    public DbSet<DeviceSnapshot> DeviceSnapshots => Set<DeviceSnapshot>();
    public DbSet<NetworkAdapterSnapshot> NetworkAdapters => Set<NetworkAdapterSnapshot>();
    public DbSet<CompanyEndpointNicSnapshot> CompanyEndpointNicSnapshots => Set<CompanyEndpointNicSnapshot>();
    public DbSet<StaticDeviceRecord> StaticDeviceRecords => Set<StaticDeviceRecord>();
    public DbSet<ReportingCompanyCache> ReportingCompanyCache => Set<ReportingCompanyCache>();
    public DbSet<ReportingSiteCache> ReportingSiteCache => Set<ReportingSiteCache>();
    public DbSet<ReportingAgentCache> ReportingAgentCache => Set<ReportingAgentCache>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConnectWiseManager");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "connectwise_manager.sqlite");
        // Proactively ensure IpAddress column exists early
        try { EnsureStaticDeviceIpColumn(); } catch { }
    }

    private void EnsureStaticDeviceIpColumn()
    {
        try
        {
            Database.OpenConnection();
            using var cmd = Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA table_info('StaticDeviceRecords');";
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var name = r[1]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) existingCols.Add(name!);
                }
            }
            if (!existingCols.Contains("IpAddress"))
            {
                using var alter = Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE StaticDeviceRecords ADD COLUMN IpAddress TEXT NULL;";
                alter.ExecuteNonQuery();
            }
        }
        catch { }
        finally
        {
            try { Database.CloseConnection(); } catch { }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceSnapshot>().ToTable("DeviceSnapshots");
        modelBuilder.Entity<DeviceSnapshot>().HasKey(x => x.Id);

        modelBuilder.Entity<NetworkAdapterSnapshot>().ToTable("NetworkAdapters");
        modelBuilder.Entity<NetworkAdapterSnapshot>().HasKey(x => x.Id);
        modelBuilder.Entity<NetworkAdapterSnapshot>()
            .HasOne<DeviceSnapshot>()
            .WithMany(d => d.Network)
            .HasForeignKey(n => n.DeviceSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CompanyEndpointNicSnapshot>().ToTable("CompanyEndpointNicSnapshots");
        modelBuilder.Entity<CompanyEndpointNicSnapshot>().HasKey(x => x.Id);
        modelBuilder.Entity<CompanyEndpointNicSnapshot>()
            .HasIndex(x => x.EndpointId)
            .IsUnique();

        modelBuilder.Entity<StaticDeviceRecord>().ToTable("StaticDeviceRecords");
        modelBuilder.Entity<StaticDeviceRecord>().HasKey(x => x.Id);
        modelBuilder.Entity<StaticDeviceRecord>()
            .HasIndex(x => x.Key)
            .IsUnique();
        modelBuilder.Entity<StaticDeviceRecord>()
            .HasIndex(x => x.EndpointId);

        modelBuilder.Entity<ReportingCompanyCache>().ToTable("ReportingCompanyCache");
        modelBuilder.Entity<ReportingCompanyCache>().HasKey(x => x.Id);
        modelBuilder.Entity<ReportingCompanyCache>()
            .HasIndex(x => x.CompanyId)
            .IsUnique();

        modelBuilder.Entity<ReportingSiteCache>().ToTable("ReportingSiteCache");
        modelBuilder.Entity<ReportingSiteCache>().HasKey(x => x.Id);
        modelBuilder.Entity<ReportingSiteCache>()
            .HasIndex(x => x.SiteId)
            .IsUnique();
        modelBuilder.Entity<ReportingSiteCache>()
            .HasIndex(x => new { x.CompanyId, x.SiteCode });

        modelBuilder.Entity<ReportingAgentCache>().ToTable("ReportingAgentCache");
        modelBuilder.Entity<ReportingAgentCache>().HasKey(x => x.Id);
        modelBuilder.Entity<ReportingAgentCache>()
            .HasIndex(x => new { x.ComputerName, x.SiteCode })
            .IsUnique();
        modelBuilder.Entity<ReportingAgentCache>()
            .HasIndex(x => x.MachineId);
    }

    // Lightweight schema upgrade for SQLite when model adds new nullable columns or tables
    public void EnsureSchemaUpgrades()
    {
        try
        {
            var conn = Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            bool TableExists(string table)
            {
                using var check = conn.CreateCommand();
                check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@t LIMIT 1;";
                var p = check.CreateParameter(); p.ParameterName = "@t"; p.Value = table; check.Parameters.Add(p);
                var found = check.ExecuteScalar();
                return found != null && found != DBNull.Value;
            }

            // Ensure DeviceSnapshots table
            if (!TableExists("DeviceSnapshots"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS DeviceSnapshots (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
EndpointId TEXT NOT NULL,
CapturedAtUtc TEXT NOT NULL,
ComputerName TEXT NOT NULL,
Domain TEXT NULL,
Username TEXT NULL,
OperatingSystem TEXT NOT NULL,
WindowsDirectory TEXT NULL,
CompanyId TEXT NULL,
CompanyName TEXT NULL,
SiteId TEXT NULL,
SiteName TEXT NULL,
Manufacturer TEXT NULL,
Model TEXT NULL,
Uptime TEXT NULL,
Processor TEXT NULL,
PhysicalMemoryGb REAL NULL,
VirtualMemoryGb REAL NULL,
DiskSummary TEXT NOT NULL DEFAULT ''
);";
                create.ExecuteNonQuery();
                using var idx = conn.CreateCommand();
                idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_DeviceSnapshots_EndpointId ON DeviceSnapshots(EndpointId);";
                idx.ExecuteNonQuery();
            }

            // Ensure NetworkAdapters table
            if (!TableExists("NetworkAdapters"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS NetworkAdapters (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
DeviceSnapshotId INTEGER NOT NULL,
Type TEXT NULL,
Description TEXT NULL,
MacAddress TEXT NULL,
Ip TEXT NULL,
Netmask TEXT NULL,
Gateway TEXT NULL,
DnsServers TEXT NULL,
DhcpServer TEXT NULL,
FOREIGN KEY(DeviceSnapshotId) REFERENCES DeviceSnapshots(Id) ON DELETE CASCADE
);";
                create.ExecuteNonQuery();
                using var idx = conn.CreateCommand();
                idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_NetworkAdapters_DeviceSnapshotId ON NetworkAdapters(DeviceSnapshotId);";
                idx.ExecuteNonQuery();
            }

            // Add any missing columns on DeviceSnapshots
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info('DeviceSnapshots');";
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader[1]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(name)) existing.Add(name);
                    }
                }

                void AddColumnIfMissing(string name, string type)
                {
                    if (existing.Contains(name)) return;
                    using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE DeviceSnapshots ADD COLUMN {name} {type};";
                    alter.ExecuteNonQuery();
                }

                AddColumnIfMissing("CompanyId", "TEXT NULL");
                AddColumnIfMissing("CompanyName", "TEXT NULL");
                AddColumnIfMissing("SiteId", "TEXT NULL");
                AddColumnIfMissing("SiteName", "TEXT NULL");
            }

            // Ensure CompanyEndpointNicSnapshots table
            if (!TableExists("CompanyEndpointNicSnapshots"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS CompanyEndpointNicSnapshots (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
EndpointId TEXT NOT NULL,
CapturedAtUtc TEXT NOT NULL,
CompanyId TEXT NULL,
CompanyName TEXT NULL,
FriendlyName TEXT NULL,
ActiveIp TEXT NULL,
ActiveMac TEXT NULL
);";
                create.ExecuteNonQuery();
                using var idx = conn.CreateCommand();
                idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_CompanyEndpointNicSnapshots_EndpointId ON CompanyEndpointNicSnapshots(EndpointId);";
                idx.ExecuteNonQuery();
            }

            // Ensure StaticDeviceRecords table
            if (!TableExists("StaticDeviceRecords"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS StaticDeviceRecords (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
A TEXT NOT NULL,
B TEXT NOT NULL,
C TEXT NOT NULL,
D TEXT NOT NULL,
Key TEXT NOT NULL,
E TEXT NULL,
HasAsio INTEGER NOT NULL DEFAULT 0,
HasScreenConnect INTEGER NOT NULL DEFAULT 0,
EndpointId TEXT NULL,
ScreenConnectSessionId TEXT NULL,
Company TEXT NULL,
Site TEXT NULL,
FriendlyName TEXT NULL,
ImportedAtUtc TEXT NOT NULL
);";
                create.ExecuteNonQuery();
                using var idx1 = conn.CreateCommand();
                idx1.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_StaticDeviceRecords_Key ON StaticDeviceRecords(Key);";
                idx1.ExecuteNonQuery();
                using var idx2 = conn.CreateCommand();
                idx2.CommandText = "CREATE INDEX IF NOT EXISTS IX_StaticDeviceRecords_EndpointId ON StaticDeviceRecords(EndpointId);";
                idx2.ExecuteNonQuery();
            }

            // Add IpAddress column to StaticDeviceRecords if missing
            try
            {
                var conn2 = Database.GetDbConnection();
                if (conn2.State != System.Data.ConnectionState.Open) conn2.Open();
                using var cmdInfo = conn2.CreateCommand();
                cmdInfo.CommandText = "PRAGMA table_info('StaticDeviceRecords');";
                var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var r = cmdInfo.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var name = r[1]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name)) existingCols.Add(name!);
                    }
                }
                if (!existingCols.Contains("IpAddress"))
                {
                    using var alter = conn2.CreateCommand();
                    alter.CommandText = "ALTER TABLE StaticDeviceRecords ADD COLUMN IpAddress TEXT NULL;";
                    alter.ExecuteNonQuery();
                }
            }
            catch { }

            // Ensure ReportingCompanyCache table
            if (!TableExists("ReportingCompanyCache"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS ReportingCompanyCache (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
CompanyId TEXT NOT NULL,
CompanyName TEXT NULL,
CompanyCode TEXT NULL,
LastSyncUtc TEXT NOT NULL,
CreatedAtUtc TEXT NOT NULL
);";
                create.ExecuteNonQuery();
                using var idx = conn.CreateCommand();
                idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_ReportingCompanyCache_CompanyId ON ReportingCompanyCache(CompanyId);";
                idx.ExecuteNonQuery();
            }

            // Ensure ReportingSiteCache table
            if (!TableExists("ReportingSiteCache"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS ReportingSiteCache (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
SiteId TEXT NOT NULL,
SiteCode TEXT NULL,
SiteName TEXT NULL,
CompanyId TEXT NULL,
CompanyName TEXT NULL,
LastSyncUtc TEXT NOT NULL,
CreatedAtUtc TEXT NOT NULL
);";
                create.ExecuteNonQuery();
                using var idx1 = conn.CreateCommand();
                idx1.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_ReportingSiteCache_SiteId ON ReportingSiteCache(SiteId);";
                idx1.ExecuteNonQuery();
                using var idx2 = conn.CreateCommand();
                idx2.CommandText = "CREATE INDEX IF NOT EXISTS IX_ReportingSiteCache_CompanyId_SiteCode ON ReportingSiteCache(CompanyId, SiteCode);";
                idx2.ExecuteNonQuery();
            }

            // Ensure ReportingAgentCache table
            if (!TableExists("ReportingAgentCache"))
            {
                using var create = conn.CreateCommand();
                create.CommandText = @"CREATE TABLE IF NOT EXISTS ReportingAgentCache (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
MachineId TEXT NULL,
ComputerName TEXT NOT NULL,
CompanyName TEXT NULL,
SiteName TEXT NULL,
SiteCode TEXT NULL,
SiteId TEXT NULL,
OperatingSystem TEXT NULL,
Status TEXT NULL,
LastSeen TEXT NULL,
MacAddress TEXT NULL,
IpAddress TEXT NULL,
LastSyncUtc TEXT NOT NULL,
CreatedAtUtc TEXT NOT NULL,
DataSource TEXT NOT NULL DEFAULT 'Unknown'
);";
                create.ExecuteNonQuery();
                using var idx1 = conn.CreateCommand();
                idx1.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_ReportingAgentCache_ComputerName_SiteCode ON ReportingAgentCache(ComputerName, SiteCode);";
                idx1.ExecuteNonQuery();
                using var idx2 = conn.CreateCommand();
                idx2.CommandText = "CREATE INDEX IF NOT EXISTS IX_ReportingAgentCache_MachineId ON ReportingAgentCache(MachineId);";
                idx2.ExecuteNonQuery();
            }
        }
        catch
        {
            // best-effort
        }
    }
}
