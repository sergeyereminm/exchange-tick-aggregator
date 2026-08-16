using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Deduplication;

namespace ExchangeTickAggregator.Tests.Deduplication;

public class TickDeduplicatorTests
{
    [Fact]
    public void TryAccept_returns_false_when_same_tick_is_seen_again_within_window()
    {
        var deduplicator = new TickDeduplicator(TimeSpan.FromSeconds(10));
        var tick = CreateTick("BTCUSDT", "binance-style");

        Assert.True(deduplicator.TryAccept(tick));
        Assert.False(deduplicator.TryAccept(tick));
    }

    [Fact]
    public void TryAccept_accepts_a_duplicate_fingerprint_only_once_under_concurrent_load()
    {
        var deduplicator = new TickDeduplicator(TimeSpan.FromSeconds(30));
        var tick = CreateTick("BTCUSDT", "binance-style");
        var acceptedCount = 0;

        Parallel.For(0, 64, _ =>
        {
            if (deduplicator.TryAccept(tick))
                Interlocked.Increment(ref acceptedCount);
        });

        Assert.Equal(1, acceptedCount);
        Assert.Equal(1, deduplicator.TrackedFingerprintCount);
    }

    [Fact]
    public void TryAccept_accepts_each_unique_tick_once_under_concurrent_load()
    {
        var deduplicator = new TickDeduplicator(TimeSpan.FromSeconds(30));
        const int threadCount = 8;
        const int ticksPerThread = 500;
        var acceptedCount = 0;

        Parallel.For(0, threadCount, threadIndex =>
        {
            for (var i = 0; i < ticksPerThread; i++)
            {
                var tick = new Tick(
                    $"SYM-{threadIndex}-{i}",
                    100m + i,
                    1m,
                    DateTimeOffset.UnixEpoch.AddMilliseconds(i),
                    $"source-{threadIndex}");

                if (deduplicator.TryAccept(tick))
                    Interlocked.Increment(ref acceptedCount);
            }
        });

        Assert.Equal(threadCount * ticksPerThread, acceptedCount);
    }

    [Fact]
    public void TryAccept_removes_fingerprints_after_the_deduplication_window()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2023-07-22T10:33:20Z"));
        var deduplicator = new TickDeduplicator(TimeSpan.FromSeconds(10), timeProvider);
        var tick = CreateTick("BTCUSDT", "binance-style");

        Assert.True(deduplicator.TryAccept(tick));
        Assert.Equal(1, deduplicator.TrackedFingerprintCount);

        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(11);
        Assert.True(deduplicator.TryAccept(CreateTick("ETHUSDT", "coinbase-style")));

        Assert.Equal(1, deduplicator.TrackedFingerprintCount);
        Assert.True(deduplicator.TryAccept(tick));
    }

    private static Tick CreateTick(string ticker, string source) =>
        new(
            ticker,
            42150.12m,
            0.5m,
            DateTimeOffset.Parse("2023-07-22T10:33:20Z"),
            source);

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
