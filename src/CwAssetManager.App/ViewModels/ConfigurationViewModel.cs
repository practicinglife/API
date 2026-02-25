using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.App.ViewModels;

public sealed partial class ConfigurationViewModel : ObservableObject
{
    private readonly ISecretsManager _secrets;

    // CW Manage
    [ObservableProperty] private string _manageBaseUrl = string.Empty;
    [ObservableProperty] private string _manageCompanyId = string.Empty;
    [ObservableProperty] private string _manageClientId = string.Empty;
    [ObservableProperty] private string _managePublicKey = string.Empty;
    [ObservableProperty] private string _managePrivateKey = string.Empty;

    // CW Control
    [ObservableProperty] private string _controlBaseUrl = string.Empty;
    [ObservableProperty] private string _controlClientId = string.Empty;
    [ObservableProperty] private string _controlClientSecret = string.Empty;
    [ObservableProperty] private string _controlTokenEndpoint = string.Empty;

    // CW RMM
    [ObservableProperty] private string _rmmBaseUrl = string.Empty;
    [ObservableProperty] private string _rmmApiKey = string.Empty;

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isSaving;

    public ConfigurationViewModel(ISecretsManager secrets) => _secrets = secrets;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        ManageBaseUrl      = await _secrets.GetSecretAsync("manage:baseUrl", ct) ?? string.Empty;
        ManageCompanyId    = await _secrets.GetSecretAsync("manage:companyId", ct) ?? string.Empty;
        ManageClientId     = await _secrets.GetSecretAsync("manage:clientId", ct) ?? string.Empty;
        ManagePublicKey    = await _secrets.GetSecretAsync("manage:publicKey", ct) ?? string.Empty;
        ManagePrivateKey   = await _secrets.GetSecretAsync("manage:privateKey", ct) ?? "••••••••";
        ControlBaseUrl     = await _secrets.GetSecretAsync("control:baseUrl", ct) ?? string.Empty;
        ControlClientId    = await _secrets.GetSecretAsync("control:clientId", ct) ?? string.Empty;
        ControlClientSecret = "••••••••";
        ControlTokenEndpoint = await _secrets.GetSecretAsync("control:tokenEndpoint", ct) ?? string.Empty;
        RmmBaseUrl         = await _secrets.GetSecretAsync("rmm:baseUrl", ct) ?? string.Empty;
        RmmApiKey          = "••••••••";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        IsSaving = true;
        StatusMessage = null;
        try
        {
            await _secrets.SetSecretAsync("manage:baseUrl",    ManageBaseUrl, ct);
            await _secrets.SetSecretAsync("manage:companyId",  ManageCompanyId, ct);
            await _secrets.SetSecretAsync("manage:clientId",   ManageClientId, ct);
            await _secrets.SetSecretAsync("manage:publicKey",  ManagePublicKey, ct);
            if (ManagePrivateKey != "••••••••")
                await _secrets.SetSecretAsync("manage:privateKey", ManagePrivateKey, ct);

            await _secrets.SetSecretAsync("control:baseUrl",         ControlBaseUrl, ct);
            await _secrets.SetSecretAsync("control:clientId",        ControlClientId, ct);
            await _secrets.SetSecretAsync("control:tokenEndpoint",   ControlTokenEndpoint, ct);
            if (ControlClientSecret != "••••••••")
                await _secrets.SetSecretAsync("control:clientSecret", ControlClientSecret, ct);

            await _secrets.SetSecretAsync("rmm:baseUrl", RmmBaseUrl, ct);
            if (RmmApiKey != "••••••••")
                await _secrets.SetSecretAsync("rmm:apiKey", RmmApiKey, ct);

            StatusMessage = "Configuration saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
