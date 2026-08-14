namespace ExchangeTickAggregator.Core.Reconnect;

public sealed class IdleTimeoutWatchdog
{
    private readonly TimeSpan _idleTimeout;
    private DateTimeOffset? _lastActivityAt;

    public IdleTimeoutWatchdog(TimeSpan idleTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        _idleTimeout = idleTimeout;
    }

    public void NotifyActivity(DateTimeOffset at) => _lastActivityAt = at;

    public bool HasTimedOut(DateTimeOffset at) =>
        _lastActivityAt is null || at - _lastActivityAt.Value >= _idleTimeout;
}
