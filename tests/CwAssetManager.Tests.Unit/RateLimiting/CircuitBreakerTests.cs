using CwAssetManager.Core.Enums;
using CwAssetManager.Infrastructure.RateLimiting;
using FluentAssertions;
using Xunit;

namespace CwAssetManager.Tests.Unit.RateLimiting;

public sealed class CircuitBreakerTests
{
    [Fact]
    public void InitialState_ShouldBeClosed()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.State.Should().Be(CircuitState.Closed);
        cb.IsAllowed().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_BelowThreshold_ShouldStayClosed()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void RecordFailure_AtThreshold_ShouldOpenCircuit()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Open);
        cb.IsAllowed().Should().BeFalse();
    }

    [Fact]
    public void AfterOpenDuration_ShouldTransitionToHalfOpen()
    {
        var cb = new CircuitBreaker(failureThreshold: 1, openDuration: TimeSpan.FromMilliseconds(50));
        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Open);

        // Wait for break duration to elapse
        Thread.Sleep(100);

        cb.State.Should().Be(CircuitState.HalfOpen);
        cb.IsAllowed().Should().BeTrue();
    }

    [Fact]
    public void HalfOpen_SuccessProbe_ShouldClose()
    {
        var cb = new CircuitBreaker(failureThreshold: 1, openDuration: TimeSpan.FromMilliseconds(50), halfOpenProbes: 1);
        cb.RecordFailure();
        Thread.Sleep(100);

        var _ = cb.State; // triggers transition to HalfOpen
        cb.RecordSuccess();
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void HalfOpen_FailureProbe_ShouldReopenCircuit()
    {
        var cb = new CircuitBreaker(failureThreshold: 1, openDuration: TimeSpan.FromMilliseconds(50));
        cb.RecordFailure();
        Thread.Sleep(100);

        var _ = cb.State; // triggers transition to HalfOpen
        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void RecordSuccess_WhenClosed_ShouldResetFailureCount()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess(); // resets count
        cb.RecordFailure();
        cb.RecordFailure();
        // Only 2 failures after success reset – should still be closed
        cb.State.Should().Be(CircuitState.Closed);
    }
}
