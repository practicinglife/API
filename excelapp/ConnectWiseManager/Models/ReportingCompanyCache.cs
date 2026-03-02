using System.ComponentModel.DataAnnotations;

namespace ConnectWiseManager.Models;

public class ReportingCompanyCache
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string CompanyId { get; set; } = string.Empty;
    
    public string? CompanyName { get; set; }
    
    public string? CompanyCode { get; set; }
    
    [Required]
    public DateTime LastSyncUtc { get; set; }
    
    [Required]
    public DateTime CreatedAtUtc { get; set; }
}
