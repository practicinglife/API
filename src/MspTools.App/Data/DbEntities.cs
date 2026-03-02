using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MspTools.App.Data;

[Table("devices")]
public class DbDevice
{
    [Key] public int Id { get; set; }
    public string? ComputerName { get; set; }
    public string? AgentName { get; set; }
    public string? CompanyName { get; set; }
    public string? SiteName { get; set; }
    public string? SourcePlatform { get; set; }
    public string? SourceId { get; set; }
    public Guid ApiConnectionId { get; set; }
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("companies")]
public class DbCompany
{
    [Key] public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyIdentifier { get; set; }
    public string? SourcePlatform { get; set; }
    public string? SourceId { get; set; }
    public Guid ApiConnectionId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    /// <summary>Pipe-separated list of site names.</summary>
    public string? SiteNames { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("matches")]
public class DbMatch
{
    [Key] public int Id { get; set; }
    public string? MatchKey { get; set; }
    /// <summary>Stored as the enum name string.</summary>
    public string? MatchType { get; set; }
    public int DeviceCount { get; set; }
    public int CompanyCount { get; set; }
    public double ConfidenceScore { get; set; }
    /// <summary>Comma-separated platform names.</summary>
    public string? PlatformList { get; set; }
    public DateTime MatchedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}
