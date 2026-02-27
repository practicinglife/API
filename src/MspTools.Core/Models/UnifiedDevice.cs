namespace MspTools.Core.Models;

/// <summary>
/// Normalized device/computer record ingested from any connected platform.
/// Fields are populated on a best-effort basis from each source API.
/// </summary>
public class UnifiedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Identity fields used for cross-platform matching ---
    public string ComputerName { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;

    // --- Source tracking ---
    public string SourcePlatform { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public Guid ApiConnectionId { get; set; }

    // --- Device metadata ---
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? SerialNumber { get; set; }
    public string? Domain { get; set; }
    public bool? IsOnline { get; set; }
    public DateTime? LastSeenUtc { get; set; }

    // --- Raw payload for extensibility ---
    public Dictionary<string, object?> RawAttributes { get; set; } = new();

    /// <summary>Ingestion timestamp.</summary>
    public DateTime IngestedAtUtc { get; set; } = DateTime.UtcNow;
}
