namespace ConnectWiseManager.Models;

public class EndpointDetail
{
    public string EndpointId { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public string? WindowsDirectory { get; set; }
    
    // Company/Site
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? SiteId { get; set; }
    public string? SiteName { get; set; }

    // System
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Uptime { get; set; }
    public string? Processor { get; set; }
    public double? PhysicalMemoryGb { get; set; }
    public double? VirtualMemoryGb { get; set; }
    public List<string> DiskVolumes { get; set; } = new();

    // Network adapters
    public List<NetworkAdapter> Network { get; set; } = new();
}

public class NetworkAdapter
{
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? MacAddress { get; set; }
    public string? Ip { get; set; }
    public string? Netmask { get; set; }
    public string? Gateway { get; set; }
    public List<string> DnsServers { get; set; } = new();
    public string? DhcpServer { get; set; }
}
