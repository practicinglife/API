using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;
using System.Collections.ObjectModel;

namespace CwAssetManager.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IAssetRepository _repo;

    [ObservableProperty] private int _totalMachines;
    [ObservableProperty] private int _onlineMachines;
    [ObservableProperty] private int _offlineMachines;
    [ObservableProperty] private int _unknownMachines;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<Machine> RecentMachines { get; } = [];

    public DashboardViewModel(IAssetRepository repo) => _repo = repo;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var machines = await _repo.GetAllAsync(ct);
            TotalMachines = machines.Count;
            OnlineMachines = machines.Count(m => m.Status == Core.Enums.MachineStatus.Online);
            OfflineMachines = machines.Count(m => m.Status == Core.Enums.MachineStatus.Offline);
            UnknownMachines = machines.Count(m => m.Status == Core.Enums.MachineStatus.Unknown);

            RecentMachines.Clear();
            foreach (var m in machines.OrderByDescending(m => m.LastSeen).Take(25))
                RecentMachines.Add(m);
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
}
