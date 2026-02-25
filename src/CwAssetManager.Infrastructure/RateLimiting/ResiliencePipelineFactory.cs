using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace CwAssetManager.Infrastructure.RateLimiting;

/// <summary>
/// Builds Polly v8 resilience pipelines combining retry (exponential back-off + jitter),
/// circuit-breaker, and timeout. Handles HTTP 429 Retry-After headers.
/// </summary>
public static class ResiliencePipelineFactory
{
    /// <summary>
    /// Creates a standard pipeline for outbound HTTP calls to a CW provider.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpPipeline(
        string providerName,
        ILogger? logger = null,
        int retryCount = 3,
        int circuitBreakerThreshold = 5,
        TimeSpan? timeout = null)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // 1. Overall timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        });

        // 2. Retry with exponential back-off + jitter
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = retryCount,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.TooManyRequests),
            OnRetry = async args =>
            {
                // Honor Retry-After header when present
                if (args.Outcome.Result?.Headers.RetryAfter is { } ra)
                {
                    var wait = ra.Delta ?? (ra.Date.HasValue ? ra.Date.Value - DateTimeOffset.UtcNow : null);
                    if (wait.HasValue && wait.Value > TimeSpan.Zero)
                        await Task.Delay(wait.Value, args.Context.CancellationToken).ConfigureAwait(false);
                }
                logger?.LogWarning("[{Provider}] Retry {Attempt} after {Delay:N0}ms",
                    providerName, args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
            }
        });

        // 3. Circuit breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = circuitBreakerThreshold,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode >= 500),
            OnOpened = args =>
            {
                logger?.LogError("[{Provider}] Circuit OPENED – break duration {Duration}",
                    providerName, args.BreakDuration);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                logger?.LogInformation("[{Provider}] Circuit CLOSED", providerName);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                logger?.LogInformation("[{Provider}] Circuit HALF-OPEN – probing", providerName);
                return ValueTask.CompletedTask;
            }
        });

        return builder.Build();
    }
}
