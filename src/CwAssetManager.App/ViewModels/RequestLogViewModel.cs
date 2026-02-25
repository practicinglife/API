using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CwAssetManager.Data;
using CwAssetManager.Data.Repositories;
using System.Collections.ObjectModel;

namespace CwAssetManager.App.ViewModels;

public sealed partial class RequestLogViewModel : ObservableObject
{
    private readonly RequestLogRepository _logRepo;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _filterProvider = string.Empty;
    [ObservableProperty] private int _fetchCount = 100;

    public ObservableCollection<RequestLog> Logs { get; } = [];

    public RequestLogViewModel(RequestLogRepository logRepo) => _logRepo = logRepo;

    [RelayCommand]
    private async Task LoadLogsAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            IReadOnlyList<RequestLog> logs;
            if (string.IsNullOrWhiteSpace(FilterProvider))
                logs = await _logRepo.GetRecentAsync(FetchCount, ct);
            else
                logs = await _logRepo.GetByProviderAsync(FilterProvider, FetchCount, ct);

            Logs.Clear();
            foreach (var l in logs)
                Logs.Add(l);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PurgeOldLogsAsync(CancellationToken ct)
    {
        await _logRepo.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-30), ct);
        await LoadLogsAsync(ct);
    }
}
