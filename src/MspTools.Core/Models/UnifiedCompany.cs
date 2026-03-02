namespace MspTools.Core.Models;

/// <summary>
/// Normalized company/organization record ingested from any connected platform.
/// </summary>
public class UnifiedCompany
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Identity fields used for cross-platform matching ---
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyIdentifier { get; set; }

    // --- Source tracking ---
    public string SourcePlatform { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public Guid ApiConnectionId { get; set; }

    // --- Company metadata ---
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }

    // --- Sites / locations under this company ---
    public List<string> SiteNames { get; set; } = new();

    /// <summary>Raw data for extensibility.</summary>
    public Dictionary<string, object?> RawAttributes { get; set; } = new();

    public DateTime IngestedAtUtc { get; set; } = DateTime.UtcNow;
}
