using CwAssetManager.Core.Interfaces;
using CwAssetManager.Core.Models;

namespace CwAssetManager.Infrastructure.RateLimiting;

/// <summary>
/// Thread-safe token-bucket rate limiter.
/// Tokens are refilled continuously based on elapsed time since last refill.
/// </summary>
public sealed class TokenBucketRateLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimitSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private double _tokens;
    private DateTimeOffset _lastRefill;
    private bool _disposed;

    public TokenBucketRateLimiter(RateLimitSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _tokens = settings.InitialTokens;
        _lastRefill = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public int Capacity => _settings.Capacity;

    /// <inheritdoc/>
    public double AvailableTokens
    {
        get
        {
            _lock.Wait();
            try { Refill(); return _tokens; }
            finally { _lock.Release(); }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> AcquireAsync(CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + _settings.MaxWait;

        while (!ct.IsCancellationRequested)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
            }
            finally
            {
                _lock.Release();
            }

            if (DateTimeOffset.UtcNow >= deadline)
                return false;

            // Wait roughly one token-refill interval before retrying
            var delay = TimeSpan.FromSeconds(1.0 / Math.Max(_settings.RefillRatePerSecond, 0.01));
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, 200));
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        return false;
    }

    private void Refill()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        if (elapsed > 0)
        {
            _tokens = Math.Min(_settings.Capacity, _tokens + elapsed * _settings.RefillRatePerSecond);
            _lastRefill = now;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _lock.Dispose();
            _disposed = true;
        }
    }
}
