namespace ExchangeTickAggregator.Core.Deduplication;

public sealed class TickDeduplicator
{
    private readonly TimeSpan _window;
    private readonly Dictionary<TickFingerprint, DateTimeOffset> _acceptedAt = new();

    public TickDeduplicator(TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _window = window;
    }

    public bool TryAccept(Tick tick)
    {
        var fingerprint = TickFingerprint.From(tick);
        var now = DateTimeOffset.UtcNow;

        if (_acceptedAt.TryGetValue(fingerprint, out var acceptedAt) &&
            now - acceptedAt <= _window)
        {
            return false;
        }

        _acceptedAt[fingerprint] = now;
        return true;
    }
}
