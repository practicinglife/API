using CommunityToolkit.Mvvm.ComponentModel;
using CwAssetManager.Core.Enums;
using CwAssetManager.Core.Interfaces;

namespace CwAssetManager.App.ViewModels;

public sealed partial class ProviderStatusViewModel : ObservableObject
{
    [ObservableProperty] private string _providerName = string.Empty;
    [ObservableProperty] private CircuitState _circuitState = CircuitState.Closed;
    [ObservableProperty] private double _availableTokens;
    [ObservableProperty] private int _queueDepth;
    [ObservableProperty] private double _requestsPerMinute;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _lastError;

    public string CircuitStateDisplay => CircuitState switch
    {
        CircuitState.Closed   => "✔ Closed",
        CircuitState.Open     => "✖ Open",
        CircuitState.HalfOpen => "⚡ Half-Open",
        _ => "Unknown"
    };

    public string CircuitStateColour => CircuitState switch
    {
        CircuitState.Closed   => "#107C10",
        CircuitState.Open     => "#C50F1F",
        CircuitState.HalfOpen => "#CA5010",
        _ => "#5A5A5A"
    };

    partial void OnCircuitStateChanged(CircuitState value)
    {
        OnPropertyChanged(nameof(CircuitStateDisplay));
        OnPropertyChanged(nameof(CircuitStateColour));
    }

    public void Refresh(IRateLimiter rateLimiter, CircuitState circuitState)
    {
        AvailableTokens = rateLimiter.AvailableTokens;
        CircuitState    = circuitState;
    }
}
