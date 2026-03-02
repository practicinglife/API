using Microsoft.EntityFrameworkCore;
using MspTools.App.ViewModels;
using MspTools.Core.Models;

namespace MspTools.App.Data;

/// <summary>
/// Persists and loads unified data (devices, companies, matches) to/from MySQL.
/// A new <see cref="MspToolsDbContext"/> is created per operation to keep connections short-lived.
/// </summary>
public sealed class LocalRepository
{
    private readonly string _connectionString;

    public LocalRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Ensures the MySQL database schema exists.
    /// Safe to call on every startup — only creates tables that are missing.
    /// </summary>
    public async Task EnsureSchemaAsync()
    {
        using var db = new MspToolsDbContext(_connectionString);
        await db.Database.EnsureCreatedAsync();
    }

    // ── Save ──────────────────────────────────────────────────────────────

    /// <summary>Replaces all stored devices from the given platform with the fresh sync data.</summary>
    public async Task SaveDevicesAsync(IEnumerable<UnifiedDevice> devices, string sourcePlatform)
    {
        using var db = new MspToolsDbContext(_connectionString);
        db.Devices.RemoveRange(db.Devices.Where(d => d.SourcePlatform == sourcePlatform));

        var now = DateTime.UtcNow;
        db.Devices.AddRange(devices.Select(d => new DbDevice
        {
            ComputerName = d.ComputerName,
            AgentName = d.AgentName,
            CompanyName = d.CompanyName,
            SiteName = d.SiteName,
            SourcePlatform = d.SourcePlatform,
            SourceId = d.SourceId,
            ApiConnectionId = d.ApiConnectionId,
            OperatingSystem = d.OperatingSystem,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress,
            SerialNumber = d.SerialNumber,
            IsOnline = d.IsOnline ?? false,
            LastSeenUtc = d.LastSeenUtc,
            SyncedAtUtc = now,
        }));

        await db.SaveChangesAsync();
    }

    /// <summary>Replaces all stored companies from the given platform with the fresh sync data.</summary>
    public async Task SaveCompaniesAsync(IEnumerable<UnifiedCompany> companies, string sourcePlatform)
    {
        using var db = new MspToolsDbContext(_connectionString);
        db.Companies.RemoveRange(db.Companies.Where(c => c.SourcePlatform == sourcePlatform));

        var now = DateTime.UtcNow;
        db.Companies.AddRange(companies.Select(c => new DbCompany
        {
            CompanyName = c.CompanyName,
            CompanyIdentifier = c.CompanyIdentifier,
            SourcePlatform = c.SourcePlatform,
            SourceId = c.SourceId,
            ApiConnectionId = c.ApiConnectionId,
            PhoneNumber = c.PhoneNumber,
            Website = c.Website,
            City = c.City,
            State = c.State,
            Country = c.Country,
            SiteNames = c.SiteNames.Count > 0 ? string.Join("|", c.SiteNames) : null,
            SyncedAtUtc = now,
        }));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Saves a summary snapshot of the current matches.
    /// Takes <see cref="MatchViewModel"/> directly since it carries the computed
    /// DeviceCount, CompanyCount, and PlatformList properties.
    /// </summary>
    public async Task SaveMatchesAsync(IEnumerable<MatchViewModel> matches)
    {
        using var db = new MspToolsDbContext(_connectionString);
        db.Matches.RemoveRange(db.Matches);

        var now = DateTime.UtcNow;
        db.Matches.AddRange(matches.Select(m => new DbMatch
        {
            MatchKey = m.MatchKey,
            MatchType = m.MatchType.ToString(),
            DeviceCount = m.DeviceCount,
            CompanyCount = m.CompanyCount,
            ConfidenceScore = m.ConfidenceScore,
            PlatformList = m.PlatformList,
            MatchedAtUtc = m.MatchedAtUtc,
            SyncedAtUtc = now,
        }));

        await db.SaveChangesAsync();
    }

    // ── Load ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UnifiedDevice>> LoadDevicesAsync()
    {
        using var db = new MspToolsDbContext(_connectionString);
        var rows = await db.Devices.AsNoTracking().ToListAsync();
        return rows.Select(d => new UnifiedDevice
        {
            ComputerName = d.ComputerName ?? string.Empty,
            AgentName = d.AgentName ?? string.Empty,
            CompanyName = d.CompanyName ?? string.Empty,
            SiteName = d.SiteName ?? string.Empty,
            SourcePlatform = d.SourcePlatform ?? string.Empty,
            SourceId = d.SourceId ?? string.Empty,
            ApiConnectionId = d.ApiConnectionId,
            OperatingSystem = d.OperatingSystem,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress,
            SerialNumber = d.SerialNumber,
            IsOnline = d.IsOnline,
            LastSeenUtc = d.LastSeenUtc,
        }).ToList();
    }

    public async Task<IReadOnlyList<UnifiedCompany>> LoadCompaniesAsync()
    {
        using var db = new MspToolsDbContext(_connectionString);
        var rows = await db.Companies.AsNoTracking().ToListAsync();
        return rows.Select(c =>
        {
            var company = new UnifiedCompany
            {
                CompanyName = c.CompanyName ?? string.Empty,
                CompanyIdentifier = c.CompanyIdentifier,
                SourcePlatform = c.SourcePlatform ?? string.Empty,
                SourceId = c.SourceId ?? string.Empty,
                ApiConnectionId = c.ApiConnectionId,
                PhoneNumber = c.PhoneNumber,
                Website = c.Website,
                City = c.City,
                State = c.State,
                Country = c.Country,
            };
            if (!string.IsNullOrWhiteSpace(c.SiteNames))
                foreach (var s in c.SiteNames.Split('|'))
                    company.SiteNames.Add(s);
            return company;
        }).ToList();
    }
}
