namespace MspTools.App.ViewModels;

public sealed class DeviceSearchCriteria : ViewModelBase
{
    private string _computerName = string.Empty;
    private string _agentName = string.Empty;
    private string _companyName = string.Empty;
    private string _siteName = string.Empty;

    public string ComputerName { get => _computerName; set => SetProperty(ref _computerName, value); }
    public string AgentName { get => _agentName; set => SetProperty(ref _agentName, value); }
    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
    public string SiteName { get => _siteName; set => SetProperty(ref _siteName, value); }
}

public sealed class CompanySearchCriteria : ViewModelBase
{
    private string _companyName = string.Empty;
    private string _siteName = string.Empty;

    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
    public string SiteName { get => _siteName; set => SetProperty(ref _siteName, value); }
}
