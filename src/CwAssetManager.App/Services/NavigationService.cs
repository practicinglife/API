using System.Windows;

namespace CwAssetManager.App.Services;

/// <summary>Simple WPF navigation service that raises an event when the target view changes.</summary>
public sealed class NavigationService
{
    public event Action<string>? NavigationRequested;

    public void NavigateTo(string viewName)
        => NavigationRequested?.Invoke(viewName);
}
