using System.ComponentModel.DataAnnotations;

namespace ConnectWiseManager.Models;

public class ReportingAgentCache
{
    [Key]
    public int Id { get; set; }
    
    public string? MachineId { get; set; }
    
    [Required]
    public string ComputerName { get; set; } = string.Empty;
    
    public string? CompanyName { get; set; }
    
    public string? SiteName { get; set; }
    
    public string? SiteCode { get; set; }
    
    public string? SiteId { get; set; }
    
    public string? OperatingSystem { get; set; }
    
    public string? Status { get; set; }
    
    public DateTime? LastSeen { get; set; }
    
    public string? MacAddress { get; set; }
    
    public string? IpAddress { get; set; }
    
    [Required]
    public DateTime LastSyncUtc { get; set; }
    
    [Required]
    public DateTime CreatedAtUtc { get; set; }
    
    [Required]
    public string DataSource { get; set; } = "Unknown"; // "Excel", "API", "Manual"
}
