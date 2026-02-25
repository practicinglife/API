namespace CwAssetManager.Core.Interfaces;

/// <summary>Token-bucket rate limiter interface.</summary>
public interface IRateLimiter
{
    /// <summary>Acquires a permit, waiting if necessary up to the configured max wait time.</summary>
    /// <returns>True if acquired, false if timed out.</returns>
    Task<bool> AcquireAsync(CancellationToken ct = default);

    /// <summary>Returns the current number of available tokens.</summary>
    double AvailableTokens { get; }

    /// <summary>Returns the configured capacity.</summary>
    int Capacity { get; }
}
