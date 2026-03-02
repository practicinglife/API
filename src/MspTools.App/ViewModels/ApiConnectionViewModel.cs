using MspTools.Core.Models;

namespace MspTools.App.ViewModels;

/// <summary>Wraps <see cref="ApiConnection"/> for display in the Connections grid.</summary>
public sealed class ApiConnectionViewModel : ViewModelBase
{
    public ApiConnection Model { get; }

    public ApiConnectionViewModel(ApiConnection model) => Model = model;

    public Guid Id => Model.Id;
    public string Name { get => Model.Name; set { Model.Name = value; OnPropertyChanged(); } }
    public ConnectorType ConnectorType { get => Model.ConnectorType; set { Model.ConnectorType = value; OnPropertyChanged(); } }
    public string BaseUrl { get => Model.BaseUrl; set { Model.BaseUrl = value; OnPropertyChanged(); } }
    public bool IsEnabled { get => Model.IsEnabled; set { Model.IsEnabled = value; OnPropertyChanged(); } }
    public DateTime LastSyncUtc { get => Model.LastSyncUtc; set { Model.LastSyncUtc = value; OnPropertyChanged(); } }
    public string? Notes { get => Model.Notes; set { Model.Notes = value; OnPropertyChanged(); } }
}
