using LLMWiki.Core.Git;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Tests;

[TestFixture]
public class CircuitBreakerTests
{
    [Test]
    public void OpensAfterThreshold()
    {
        var clock = new FakeClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 5, cooldown: TimeSpan.FromMinutes(5));

        for (var i = 0; i < 4; i++) cb.RecordFailure();
        cb.State.Should().Be(CircuitBreakerState.Closed);

        cb.RecordFailure();
        cb.State.Should().Be(CircuitBreakerState.Open);
        cb.CanProceed().Should().BeFalse();
    }

    [Test]
    public void RemainsOpenForCooldown_ThenAutoCloses()
    {
        var clock = new FakeClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 2, cooldown: TimeSpan.FromMinutes(5));

        cb.RecordFailure();
        cb.RecordFailure();
        cb.State.Should().Be(CircuitBreakerState.Open);

        clock.Advance(TimeSpan.FromMinutes(4));
        cb.CanProceed().Should().BeFalse();

        clock.Advance(TimeSpan.FromMinutes(2));
        cb.CanProceed().Should().BeTrue();
        cb.State.Should().Be(CircuitBreakerState.Closed);
    }

    [Test]
    public void SuccessResetsFailureCount()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.State.Should().Be(CircuitBreakerState.Closed);
    }

    [Test]
    public void Reset_ForcesClosed()
    {
        var clock = new FakeClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 1);
        cb.RecordFailure();
        cb.State.Should().Be(CircuitBreakerState.Open);

        cb.Reset();
        cb.CanProceed().Should().BeTrue();
    }
}
