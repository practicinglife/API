using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace ConnectWiseManager.Models;

public class Device : INotifyPropertyChanged
{
    private string _computerName = string.Empty;
    private string _companyName = string.Empty;
    private string _siteName = string.Empty;
    private string _macAddress = string.Empty;

    private string? _screenConnectName;
    private string? _screenConnectMac;
    private string? _screenConnectCompany;
    private string? _screenConnectSite;
    private bool _screenConnectMatched;

    private List<string> _allMacAddresses = new();

    public string Id { get; set; } = string.Empty;

    public string ComputerName
    {
        get => _computerName;
        set { if (_computerName != value) { _computerName = value; OnPropertyChanged(); } }
    }

    public string CompanyName
    {
        get => _companyName;
        set { if (_companyName != value) { _companyName = value; OnPropertyChanged(); } }
    }

    public string SiteName
    {
        get => _siteName;
        set { if (_siteName != value) { _siteName = value; OnPropertyChanged(); } }
    }

    public string MacAddress
    {
        get => _macAddress;
        set { if (_macAddress != value) { _macAddress = value; OnPropertyChanged(); } }
    }

    // All discovered MAC addresses from Asio endpoint details (active and inactive)
    public List<string> AllMacAddresses
    {
        get => _allMacAddresses;
        set
        {
            if (!ReferenceEquals(_allMacAddresses, value))
            {
                _allMacAddresses = value ?? new();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AllMacsJoined));
            }
        }
    }

    // Convenience string for UI listing of all MACs
    public string AllMacsJoined => _allMacAddresses == null || _allMacAddresses.Count == 0
        ? string.Empty
        : string.Join(", ", _allMacAddresses);

    public string DeviceType { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastSeen { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();

    // ScreenConnect comparison fields
    public string? ScreenConnectName
    {
        get => _screenConnectName;
        set { if (_screenConnectName != value) { _screenConnectName = value; OnPropertyChanged(); } }
    }

    public string? ScreenConnectMac
    {
        get => _screenConnectMac;
        set { if (_screenConnectMac != value) { _screenConnectMac = value; OnPropertyChanged(); } }
    }

    public string? ScreenConnectCompany
    {
        get => _screenConnectCompany;
        set { if (_screenConnectCompany != value) { _screenConnectCompany = value; OnPropertyChanged(); } }
    }

    public string? ScreenConnectSite
    {
        get => _screenConnectSite;
        set { if (_screenConnectSite != value) { _screenConnectSite = value; OnPropertyChanged(); } }
    }

    public bool ScreenConnectMatched
    {
        get => _screenConnectMatched;
        set { if (_screenConnectMatched != value) { _screenConnectMatched = value; OnPropertyChanged(); } }
    }

    // Convenience display field
    public string CompanySiteEndpoint => $"{CompanyName}/{SiteName}/{ComputerName}";

    // Segmentation helpers
    public string Category
    {
        get
        {
            var os = OperatingSystem ?? string.Empty;
            if (os.IndexOf("server", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Server";
            if (os.IndexOf("mac", System.StringComparison.OrdinalIgnoreCase) >= 0 || os.IndexOf("darwin", System.StringComparison.OrdinalIgnoreCase) >= 0 || os.IndexOf("os x", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Mac";
            if (os.IndexOf("windows", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Windows";
            if (os.IndexOf("linux", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Linux";
            return "Unknown";
        }
    }

    public string ServerVersion
    {
        get
        {
            var os = OperatingSystem ?? string.Empty;
            if (os.IndexOf("server", System.StringComparison.OrdinalIgnoreCase) < 0) return string.Empty;
            // Try to extract version year or number
            var known = new[] { "2025", "2022", "2019", "2016", "2012", "2008" };
            foreach (var k in known)
            {
                if (os.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return k;
            }
            return string.Empty;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
