using System.Collections.Concurrent;

namespace ExchangeTickAggregator.Core.Deduplication;

public sealed class TickDeduplicator
{
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<TickFingerprint, DateTimeOffset> _acceptedAt = new();

    public TickDeduplicator(TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _window = window;
    }

    public bool TryAccept(Tick tick)
    {
        var fingerprint = TickFingerprint.From(tick);
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            if (_acceptedAt.TryGetValue(fingerprint, out var acceptedAt))
            {
                if (now - acceptedAt <= _window)
                    return false;

                if (_acceptedAt.TryUpdate(fingerprint, now, acceptedAt))
                    return true;

                continue;
            }

            if (_acceptedAt.TryAdd(fingerprint, now))
                return true;
        }
    }
}
