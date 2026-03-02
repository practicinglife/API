using System.ComponentModel.DataAnnotations;

namespace ConnectWiseManager.Models;

public class ReportingSiteCache
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string SiteId { get; set; } = string.Empty;
    
    public string? SiteCode { get; set; }
    
    public string? SiteName { get; set; }
    
    public string? CompanyId { get; set; }
    
    public string? CompanyName { get; set; }
    
    [Required]
    public DateTime LastSyncUtc { get; set; }
    
    [Required]
    public DateTime CreatedAtUtc { get; set; }
}
