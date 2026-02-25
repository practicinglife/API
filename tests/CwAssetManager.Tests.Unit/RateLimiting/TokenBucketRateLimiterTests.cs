using CwAssetManager.Core.Models;
using CwAssetManager.Infrastructure.RateLimiting;
using FluentAssertions;
using Xunit;

namespace CwAssetManager.Tests.Unit.RateLimiting;

public sealed class TokenBucketRateLimiterTests
{
    private static RateLimitSettings FastSettings(int capacity = 5, double refillRate = 100) =>
        new() { Capacity = capacity, RefillRatePerSecond = refillRate, InitialTokens = capacity, MaxWait = TimeSpan.FromSeconds(2) };

    [Fact]
    public async Task AcquireAsync_WithAvailableTokens_ShouldReturnTrue()
    {
        var limiter = new TokenBucketRateLimiter(FastSettings(capacity: 5));
        var result = await limiter.AcquireAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_ExhaustingAllTokens_ShouldEventuallyRefill()
    {
        // High refill rate so tokens replenish quickly
        var settings = new RateLimitSettings
        {
            Capacity = 2,
            InitialTokens = 2,
            RefillRatePerSecond = 100,  // very fast refill
            MaxWait = TimeSpan.FromSeconds(5)
        };
        var limiter = new TokenBucketRateLimiter(settings);

        // Drain
        (await limiter.AcquireAsync()).Should().BeTrue();
        (await limiter.AcquireAsync()).Should().BeTrue();

        // Tokens should refill and allow another acquire
        var result = await limiter.AcquireAsync();
        result.Should().BeTrue("tokens should have refilled");
    }

    [Fact]
    public async Task AcquireAsync_WithZeroTokensAndNoRefill_ShouldTimeOut()
    {
        var settings = new RateLimitSettings
        {
            Capacity = 1,
            InitialTokens = 1,
            RefillRatePerSecond = 0.001, // extremely slow refill
            MaxWait = TimeSpan.FromMilliseconds(200)
        };
        var limiter = new TokenBucketRateLimiter(settings);

        // Drain the single token
        await limiter.AcquireAsync();

        // Next acquire should time out
        var result = await limiter.AcquireAsync();
        result.Should().BeFalse("max wait should have expired");
    }

    [Fact]
    public void Capacity_ShouldMatchSettings()
    {
        var limiter = new TokenBucketRateLimiter(FastSettings(capacity: 42));
        limiter.Capacity.Should().Be(42);
    }

    [Fact]
    public void AvailableTokens_InitiallyEqualsInitialTokens()
    {
        var settings = new RateLimitSettings { Capacity = 10, InitialTokens = 7, RefillRatePerSecond = 0 };
        var limiter = new TokenBucketRateLimiter(settings);
        limiter.AvailableTokens.Should().BeApproximately(7, 0.1);
    }

    [Fact]
    public async Task AcquireAsync_CancelledToken_ShouldThrow()
    {
        var settings = new RateLimitSettings
        {
            Capacity = 1,
            InitialTokens = 0,
            RefillRatePerSecond = 0.001,
            MaxWait = TimeSpan.FromSeconds(60)
        };
        var limiter = new TokenBucketRateLimiter(settings);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        var act = () => limiter.AcquireAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
