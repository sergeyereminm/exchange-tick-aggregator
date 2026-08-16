namespace ExchangeTickAggregator.Core.Reconnect;

public sealed class ExponentialBackoffPolicy
{
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _factor;
    private readonly Func<double> _jitterUnit;
    private TimeSpan _nextDelay;

    public ExponentialBackoffPolicy(
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double factor,
        Func<double>? jitterUnit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, initialDelay);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(factor, 1.0);

        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
        _factor = factor;
        _jitterUnit = jitterUnit ?? Random.Shared.NextDouble;
        _nextDelay = initialDelay;
    }

    public TimeSpan NextDelay()
    {
        var unit = _jitterUnit();
        ArgumentOutOfRangeException.ThrowIfLessThan(unit, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unit, 1.0);

        var jitteredMilliseconds = _nextDelay.TotalMilliseconds * (0.5 + (0.5 * unit));
        var delay = TimeSpan.FromMilliseconds(jitteredMilliseconds);

        var grownMilliseconds = _nextDelay.TotalMilliseconds * _factor;
        _nextDelay = TimeSpan.FromMilliseconds(Math.Min(grownMilliseconds, _maxDelay.TotalMilliseconds));
        return delay;
    }

    public void Reset()
    {
        _nextDelay = _initialDelay;
    }
}
