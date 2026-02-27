using MspTools.Core.Interfaces;
using MspTools.Core.Models;

namespace MspTools.Core.Models;

/// <summary>
/// Thread-safe in-memory container for all ingested MSP platform data.
/// Supports searching by computer name, agent name, company name, and site name,
/// and computes cross-platform matches to identify the same asset across multiple tools.
/// </summary>
public sealed class DataContainer : IDataContainer
{
    private readonly List<UnifiedDevice> _devices = new();
    private readonly List<UnifiedCompany> _companies = new();
    private readonly List<CrossPlatformMatch> _matches = new();
    private readonly object _lock = new();

    public IReadOnlyList<UnifiedDevice> Devices { get { lock (_lock) return _devices.AsReadOnly(); } }
    public IReadOnlyList<UnifiedCompany> Companies { get { lock (_lock) return _companies.AsReadOnly(); } }
    public IReadOnlyList<CrossPlatformMatch> Matches { get { lock (_lock) return _matches.AsReadOnly(); } }

    public event EventHandler? DataChanged;

    public void IngestDevices(IEnumerable<UnifiedDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        var list = devices.ToList();
        lock (_lock)
        {
            // Replace existing records from the same source
            var sourceIds = list.Select(d => (d.ApiConnectionId, d.SourceId)).ToHashSet();
            _devices.RemoveAll(d => sourceIds.Contains((d.ApiConnectionId, d.SourceId)));
            _devices.AddRange(list);
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void IngestCompanies(IEnumerable<UnifiedCompany> companies)
    {
        ArgumentNullException.ThrowIfNull(companies);
        var list = companies.ToList();
        lock (_lock)
        {
            var sourceIds = list.Select(c => (c.ApiConnectionId, c.SourceId)).ToHashSet();
            _companies.RemoveAll(c => sourceIds.Contains((c.ApiConnectionId, c.SourceId)));
            _companies.AddRange(list);
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<UnifiedDevice> SearchDevices(string? computerName = null, string? agentName = null,
        string? companyName = null, string? siteName = null)
    {
        lock (_lock)
        {
            return _devices
                .Where(d =>
                    (computerName == null || d.ComputerName.Contains(computerName, StringComparison.OrdinalIgnoreCase)) &&
                    (agentName == null || d.AgentName.Contains(agentName, StringComparison.OrdinalIgnoreCase)) &&
                    (companyName == null || d.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase)) &&
                    (siteName == null || d.SiteName.Contains(siteName, StringComparison.OrdinalIgnoreCase)))
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<UnifiedCompany> SearchCompanies(string? companyName = null, string? siteName = null)
    {
        lock (_lock)
        {
            return _companies
                .Where(c =>
                    (companyName == null || c.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase)) &&
                    (siteName == null || c.SiteNames.Any(s => s.Contains(siteName, StringComparison.OrdinalIgnoreCase))))
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Groups devices by computer name (case-insensitive) across platforms and builds match records.
    /// Also matches companies by normalized company name.
    /// </summary>
    public void ComputeMatches()
    {
        lock (_lock)
        {
            _matches.Clear();

            // Match devices by computer name across multiple source platforms
            var byComputerName = _devices
                .Where(d => !string.IsNullOrWhiteSpace(d.ComputerName))
                .GroupBy(d => d.ComputerName.Trim().ToUpperInvariant())
                .Where(g => g.Select(d => d.SourcePlatform).Distinct().Count() > 1);

            foreach (var group in byComputerName)
            {
                _matches.Add(new CrossPlatformMatch
                {
                    MatchKey = group.Key,
                    MatchType = MatchType.ComputerName,
                    Devices = group.ToList(),
                    ConfidenceScore = 1.0,
                });
            }

            // Match devices by agent name across platforms
            var byAgentName = _devices
                .Where(d => !string.IsNullOrWhiteSpace(d.AgentName))
                .GroupBy(d => d.AgentName.Trim().ToUpperInvariant())
                .Where(g => g.Select(d => d.SourcePlatform).Distinct().Count() > 1);

            // Build a set of device IDs already covered by computer-name matches for O(1) lookup
            var coveredDeviceIds = _matches
                .Where(m => m.MatchType == MatchType.ComputerName)
                .SelectMany(m => m.Devices)
                .Select(d => d.Id)
                .ToHashSet();

            foreach (var group in byAgentName)
            {
                // Skip if all devices in the group are already covered by a computer name match
                if (group.All(d => coveredDeviceIds.Contains(d.Id))) continue;

                _matches.Add(new CrossPlatformMatch
                {
                    MatchKey = group.Key,
                    MatchType = MatchType.AgentName,
                    Devices = group.ToList(),
                    ConfidenceScore = 0.9,
                });
            }

            // Match companies by name across platforms
            var byCompanyName = _companies
                .Where(c => !string.IsNullOrWhiteSpace(c.CompanyName))
                .GroupBy(c => c.CompanyName.Trim().ToUpperInvariant())
                .Where(g => g.Select(c => c.SourcePlatform).Distinct().Count() > 1);

            foreach (var group in byCompanyName)
            {
                _matches.Add(new CrossPlatformMatch
                {
                    MatchKey = group.Key,
                    MatchType = MatchType.CompanyName,
                    Companies = group.ToList(),
                    ConfidenceScore = 0.95,
                });
            }
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _devices.Clear();
            _companies.Clear();
            _matches.Clear();
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
