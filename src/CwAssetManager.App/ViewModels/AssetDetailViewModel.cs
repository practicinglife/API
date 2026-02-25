using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;

namespace CwAssetManager.App.ViewModels;

public sealed partial class AssetDetailViewModel : ObservableObject
{
    private readonly IAssetRepository _repo;

    [ObservableProperty] private Machine? _machine;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public AssetDetailViewModel(IAssetRepository repo) => _repo = repo;

    [RelayCommand]
    private async Task LoadMachineAsync(Guid id, CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Machine = await _repo.GetByIdAsync(id, ct);
            if (Machine is null) ErrorMessage = $"Asset {id} not found.";
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

    public void LoadMachine(Machine machine) => Machine = machine;
}
