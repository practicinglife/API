using MspTools.Core.Authentication;
using MspTools.Core.Models;

namespace MspTools.App.ViewModels;

/// <summary>Holds form state for the "New Connection" panel.</summary>
public sealed class NewConnectionFormViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private ConnectorType _connectorType = ConnectorType.ConnectWiseManage;
    private string _baseUrl = string.Empty;
    private AuthMethodType _selectedAuthType = AuthMethodType.ConnectWiseApiKey;

    // Common fields
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ConnectorType ConnectorType { get => _connectorType; set { SetProperty(ref _connectorType, value); ApplyDefaultUrl(); } }
    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    public AuthMethodType SelectedAuthType { get => _selectedAuthType; set => SetProperty(ref _selectedAuthType, value); }

    // ConnectWise Manage / API key fields
    public string CompanyId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    // Basic auth fields
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Generic API key field
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "x-api-key";

    // Bearer token field
    public string BearerToken { get; set; } = string.Empty;

    private void ApplyDefaultUrl()
    {
        BaseUrl = _connectorType switch
        {
            ConnectorType.ConnectWiseManage => "https://na.myconnectwise.net/v4_6_release/apis/3.0",
            ConnectorType.ConnectWiseAsio => "https://openapi.service.itsupport247.net",
            ConnectorType.ConnectWiseControl => "https://your-instance.screenconnect.com/App_Extensions/fc234f0e-2e8e-4a1f-b977-ba41b14031f6",
            _ => string.Empty
        };
        SelectedAuthType = _connectorType switch
        {
            ConnectorType.ConnectWiseManage => AuthMethodType.ConnectWiseApiKey,
            ConnectorType.ConnectWiseAsio => AuthMethodType.ApiKey,
            ConnectorType.ConnectWiseControl => AuthMethodType.BasicAuth,
            _ => AuthMethodType.ApiKey
        };
    }
}
