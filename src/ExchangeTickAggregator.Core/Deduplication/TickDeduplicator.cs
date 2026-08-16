using System.Collections.Concurrent;

namespace ExchangeTickAggregator.Core.Deduplication;

public sealed class TickDeduplicator
{
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<TickFingerprint, DateTimeOffset> _acceptedAt = new();

    public TickDeduplicator(TimeSpan window, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        _window = window;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int TrackedFingerprintCount => _acceptedAt.Count;

    public bool TryAccept(Tick tick)
    {
        var fingerprint = TickFingerprint.From(tick);
        var now = _timeProvider.GetUtcNow();
        RemoveExpired(now);

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

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in _acceptedAt)
        {
            if (now - pair.Value > _window)
                _acceptedAt.TryRemove(pair.Key, out _);
        }
    }
}
