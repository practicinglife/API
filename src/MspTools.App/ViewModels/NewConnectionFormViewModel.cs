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
    public ConnectorType ConnectorType
    {
        get => _connectorType;
        set
        {
            SetProperty(ref _connectorType, value);
            ApplyDefaultUrl();
        }
    }
    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    public AuthMethodType SelectedAuthType
    {
        get => _selectedAuthType;
        set
        {
            SetProperty(ref _selectedAuthType, value);
            OnPropertyChanged(nameof(IsConnectWiseApiKeyAuth));
            OnPropertyChanged(nameof(IsBasicAuth));
            OnPropertyChanged(nameof(IsApiKeyAuth));
            OnPropertyChanged(nameof(IsBearerTokenAuth));
            OnPropertyChanged(nameof(IsClientCredentialsAuth));
        }
    }

    // Visibility helpers for credential panels
    public bool IsConnectWiseApiKeyAuth => _selectedAuthType == AuthMethodType.ConnectWiseApiKey;
    public bool IsBasicAuth => _selectedAuthType == AuthMethodType.BasicAuth;
    public bool IsApiKeyAuth => _selectedAuthType == AuthMethodType.ApiKey;
    public bool IsBearerTokenAuth => _selectedAuthType == AuthMethodType.BearerToken;
    public bool IsClientCredentialsAuth => _selectedAuthType == AuthMethodType.ClientCredentials;

    // Enum sources for ComboBoxes in the dialog
    public IEnumerable<ConnectorType> ConnectorTypes { get; } =
        Enum.GetValues<ConnectorType>().Where(t => t != ConnectorType.Custom);

    public IEnumerable<AuthMethodType> AuthMethodTypes { get; } = Enum.GetValues<AuthMethodType>();

    // ConnectWise Manage / API key fields
    public string CompanyId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    // Client credentials fields (Asio)
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>Optional OAuth2 scope(s). Defaults to required Asio scopes when ConnectorType is Asio.</summary>
    public string Scope { get; set; } = string.Empty;

    // Basic auth fields
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Generic API key field
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "x-api-key";

    // Bearer token field
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>Clears all form fields back to their defaults after a successful add.</summary>
    public void Reset()
    {
        Name = string.Empty;
        ConnectorType = ConnectorType.ConnectWiseManage;
        CompanyId = string.Empty;
        PublicKey = string.Empty;
        PrivateKey = string.Empty;
        ClientId = string.Empty;
        ClientSecret = string.Empty;
        Scope = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        ApiKey = string.Empty;
        ApiKeyHeader = "x-api-key";
        BearerToken = string.Empty;
    }

    private void ApplyDefaultUrl()
    {
        BaseUrl = _connectorType switch
        {
            ConnectorType.ConnectWiseManage => "https://na.myconnectwise.net/v4_6_release/apis/3.0",
            ConnectorType.ConnectWiseAsio => "https://openapi.service.itsupport247.net",
            ConnectorType.ConnectWiseControl => "https://your-instance.screenconnect.com/App_Extensions/fc234f0e-2e8e-4a1f-b977-ba41b14031f6",
            ConnectorType.Eset => "https://your-eset-server/era/v1",
            _ => string.Empty
        };
        SelectedAuthType = _connectorType switch
        {
            ConnectorType.ConnectWiseManage => AuthMethodType.ConnectWiseApiKey,
            ConnectorType.ConnectWiseAsio => AuthMethodType.ClientCredentials,
            ConnectorType.ConnectWiseControl => AuthMethodType.BasicAuth,
            ConnectorType.Eset => AuthMethodType.BasicAuth,
            _ => AuthMethodType.ApiKey
        };
        Scope = _connectorType == ConnectorType.ConnectWiseAsio
            ? "platform.asset.read platform.devices.read platform.companies.read platform.sites.read"
            : string.Empty;
    }
}
