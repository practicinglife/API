namespace ConnectWiseManager.Models;

public class CompanyEndpointNicSnapshot
{
    public int Id { get; set; }
    public string EndpointId { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }

    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;

    public string? ActiveIp { get; set; }
    public string? ActiveMac { get; set; }
}
