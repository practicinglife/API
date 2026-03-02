namespace ConnectWiseManager.Models;

public class ReportingSite
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteCode { get; set; }
    public string? SiteName { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }

    public string DisplayLabel
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(SiteCode) ? SiteName : $"{SiteCode} - {SiteName}";
            label = string.IsNullOrWhiteSpace(label) ? SiteId : label;
            if (!string.IsNullOrWhiteSpace(CompanyName))
            {
                return $"{label} ({CompanyName})";
            }
            return label;
        }
    }
}
