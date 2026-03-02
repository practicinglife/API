using MspTools.Core.Models;

namespace MspTools.Core.Interfaces;

/// <summary>
/// In-memory container that holds all ingested data from connected platforms
/// and provides search/match capabilities across them.
/// </summary>
public interface IDataContainer
{
    /// <summary>All devices currently held in the container.</summary>
    IReadOnlyList<UnifiedDevice> Devices { get; }

    /// <summary>All companies currently held in the container.</summary>
    IReadOnlyList<UnifiedCompany> Companies { get; }

    /// <summary>All computed cross-platform matches.</summary>
    IReadOnlyList<CrossPlatformMatch> Matches { get; }

    /// <summary>Add or replace devices from a specific source.</summary>
    void IngestDevices(IEnumerable<UnifiedDevice> devices);

    /// <summary>Add or replace companies from a specific source.</summary>
    void IngestCompanies(IEnumerable<UnifiedCompany> companies);

    /// <summary>Search devices by any combination of name fields (case-insensitive, partial match).</summary>
    IReadOnlyList<UnifiedDevice> SearchDevices(string? computerName = null, string? agentName = null,
        string? companyName = null, string? siteName = null);

    /// <summary>Search companies by name (case-insensitive, partial match).</summary>
    IReadOnlyList<UnifiedCompany> SearchCompanies(string? companyName = null, string? siteName = null);

    /// <summary>Compute cross-platform matches and populate <see cref="Matches"/>.</summary>
    void ComputeMatches();

    /// <summary>Clear all ingested data and matches.</summary>
    void Clear();

    /// <summary>Raised whenever the container contents change.</summary>
    event EventHandler? DataChanged;
}
