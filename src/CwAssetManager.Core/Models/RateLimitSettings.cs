namespace CwAssetManager.Core.Models;

/// <summary>Token-bucket rate limiter settings for a provider.</summary>
public sealed class RateLimitSettings
{
    /// <summary>Maximum number of tokens (burst capacity).</summary>
    public int Capacity { get; set; } = 60;

    /// <summary>Tokens added per second (sustained rate).</summary>
    public double RefillRatePerSecond { get; set; } = 1.0;

    /// <summary>Initial token count (defaults to capacity).</summary>
    public int InitialTokens { get; set; } = 60;

    /// <summary>Maximum wait time before returning a rate-limit exceeded error.</summary>
    public TimeSpan MaxWait { get; set; } = TimeSpan.FromSeconds(30);
}
