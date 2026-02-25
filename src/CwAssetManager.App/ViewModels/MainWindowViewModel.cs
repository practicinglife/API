using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IIngestionService _ingestion;
    private readonly NavigationService _nav;

    [ObservableProperty] private string _title = "CW Asset Manager";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isIngesting;
    [ObservableProperty] private ObservableObject? _currentView;

    public ProviderStatusViewModel ManageStatus { get; }
    public ProviderStatusViewModel ControlStatus { get; }
    public ProviderStatusViewModel RmmStatus { get; }

    public MainWindowViewModel(
        IIngestionService ingestion,
        NavigationService nav,
        ProviderStatusViewModel providerStatus)
    {
        _ingestion = ingestion;
        _nav = nav;

        ManageStatus = new ProviderStatusViewModel { ProviderName = "CW Manage" };
        ControlStatus = new ProviderStatusViewModel { ProviderName = "CW Control" };
        RmmStatus = new ProviderStatusViewModel { ProviderName = "CW RMM" };
    }

    [RelayCommand]
    private async Task RunIngestionAsync(CancellationToken ct)
    {
        if (IsIngesting) return;
        IsIngesting = true;
        StatusMessage = "Ingestion running…";
        try
        {
            await _ingestion.RunFullIngestionAsync(ct);
            StatusMessage = $"Ingestion complete – {DateTimeOffset.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Ingestion cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsIngesting = false;
        }
    }

    [RelayCommand]
    private void PauseIngestion() => _ingestion.Pause();

    [RelayCommand]
    private void ResumeIngestion() => _ingestion.Resume();

    [RelayCommand]
    private void NavigateToDashboard() => _nav.NavigateTo("Dashboard");

    [RelayCommand]
    private void NavigateToAssets() => _nav.NavigateTo("Assets");

    [RelayCommand]
    private void NavigateToRequestLog() => _nav.NavigateTo("RequestLog");

    [RelayCommand]
    private void NavigateToConfiguration() => _nav.NavigateTo("Configuration");
}
