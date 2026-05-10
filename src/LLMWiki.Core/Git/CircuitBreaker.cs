using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Core.Git;

public enum CircuitBreakerState
{
    Closed,
    Open,
}

public sealed class CircuitBreaker
{
    private readonly IClock _clock;
    private readonly int _failureThreshold;
    private readonly TimeSpan _cooldown;
    private int _consecutiveFailures;
    private DateTime? _openedAt;

    public CircuitBreaker(
        IClock? clock = null,
        int failureThreshold = 5,
        TimeSpan? cooldown = null)
    {
        _clock = clock ?? SystemClock.Instance;
        _failureThreshold = failureThreshold;
        _cooldown = cooldown ?? TimeSpan.FromMinutes(5);
    }

    public CircuitBreakerState State =>
        IsOpen() ? CircuitBreakerState.Open : CircuitBreakerState.Closed;

    public TimeSpan RemainingCooldown
    {
        get
        {
            if (_openedAt is null) return TimeSpan.Zero;
            var elapsed = _clock.UtcNow - _openedAt.Value;
            var remaining = _cooldown - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool CanProceed()
    {
        if (!IsOpen()) return true;
        if (RemainingCooldown <= TimeSpan.Zero)
        {
            _openedAt = null;
            _consecutiveFailures = 0;
            return true;
        }
        return false;
    }

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _openedAt = null;
    }

    public void RecordFailure()
    {
        if (IsOpen()) return;

        _consecutiveFailures++;
        if (_consecutiveFailures >= _failureThreshold)
        {
            _openedAt = _clock.UtcNow;
        }
    }

    public void Reset()
    {
        _consecutiveFailures = 0;
        _openedAt = null;
    }

    private bool IsOpen()
    {
        if (_openedAt is null) return false;
        var elapsed = _clock.UtcNow - _openedAt.Value;
        return elapsed < _cooldown;
    }
}
