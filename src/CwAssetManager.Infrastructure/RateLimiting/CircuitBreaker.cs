using CwAssetManager.Core.Enums;

namespace CwAssetManager.Infrastructure.RateLimiting;

/// <summary>
/// Thread-safe circuit breaker implementation with Closed, Open, and HalfOpen states.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly int _halfOpenProbes;

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private int _successCount;
    private DateTimeOffset _openedAt;
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold = 5, TimeSpan openDuration = default, int halfOpenProbes = 1)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration == default ? TimeSpan.FromSeconds(30) : openDuration;
        _halfOpenProbes = halfOpenProbes;
    }

    /// <summary>Current circuit state.</summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                TryTransitionFromOpen();
                return _state;
            }
        }
    }

    /// <summary>Returns true if calls should be allowed through.</summary>
    public bool IsAllowed()
    {
        lock (_lock)
        {
            TryTransitionFromOpen();
            return _state is CircuitState.Closed or CircuitState.HalfOpen;
        }
    }

    /// <summary>Records a successful call. Resets failure count in Closed; closes from HalfOpen.</summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _successCount++;
                if (_successCount >= _halfOpenProbes)
                    TransitionTo(CircuitState.Closed);
            }
            else if (_state == CircuitState.Closed)
            {
                _failureCount = 0;
            }
        }
    }

    /// <summary>Records a failed call. Opens the circuit when threshold is reached.</summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open)
                return;

            _failureCount++;
            if (_state == CircuitState.HalfOpen || _failureCount >= _failureThreshold)
                TransitionTo(CircuitState.Open);
        }
    }

    private void TryTransitionFromOpen()
    {
        if (_state == CircuitState.Open && DateTimeOffset.UtcNow - _openedAt >= _openDuration)
            TransitionTo(CircuitState.HalfOpen);
    }

    private void TransitionTo(CircuitState next)
    {
        _state = next;
        if (next == CircuitState.Open)
            _openedAt = DateTimeOffset.UtcNow;
        if (next == CircuitState.Closed || next == CircuitState.HalfOpen)
        {
            _failureCount = 0;
            _successCount = 0;
        }
    }
}
