namespace MspTools.Core.Models;

/// <summary>
/// Represents a cross-platform match result — devices/companies that appear to be
/// the same entity across two or more source platforms.
/// </summary>
public class CrossPlatformMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MatchKey { get; set; } = string.Empty;
    public MatchType MatchType { get; set; }
    public List<UnifiedDevice> Devices { get; set; } = new();
    public List<UnifiedCompany> Companies { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public DateTime MatchedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Which field was used as the primary match key.</summary>
public enum MatchType
{
    ComputerName,
    AgentName,
    CompanyName,
    SiteName,
    MultiField
}
