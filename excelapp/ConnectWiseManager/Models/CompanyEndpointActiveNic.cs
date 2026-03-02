namespace ConnectWiseManager.Models;

public class CompanyEndpointActiveNic
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string EndpointId { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string? ActiveMac { get; set; }
    public string? ActiveIp { get; set; }
}
