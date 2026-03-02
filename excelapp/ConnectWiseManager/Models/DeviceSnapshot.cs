namespace ConnectWiseManager.Models;

public class DeviceSnapshot
{
    public int Id { get; set; }
    public string EndpointId { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }

    public string ComputerName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public string? WindowsDirectory { get; set; }

    // Company/Site info (ids may be null when sourced from cache)
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? SiteId { get; set; }
    public string? SiteName { get; set; }

    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Uptime { get; set; }
    public string? Processor { get; set; }
    public double? PhysicalMemoryGb { get; set; }
    public double? VirtualMemoryGb { get; set; }
    public string DiskSummary { get; set; } = string.Empty; // concatenated lines

    public List<NetworkAdapterSnapshot> Network { get; set; } = new();
}

public class NetworkAdapterSnapshot
{
    public int Id { get; set; }
    public int DeviceSnapshotId { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? MacAddress { get; set; }
    public string? Ip { get; set; }
    public string? Netmask { get; set; }
    public string? Gateway { get; set; }
    public string? DnsServers { get; set; } // comma separated
    public string? DhcpServer { get; set; }
}
