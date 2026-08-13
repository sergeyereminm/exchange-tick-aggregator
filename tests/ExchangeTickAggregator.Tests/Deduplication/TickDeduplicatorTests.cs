using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Deduplication;

namespace ExchangeTickAggregator.Tests.Deduplication;

public class TickDeduplicatorTests
{
    [Fact]
    public void TryAccept_returns_false_when_same_tick_is_seen_again_within_window()
    {
        var deduplicator = new TickDeduplicator(TimeSpan.FromSeconds(10));
        var tick = new Tick(
            "BTCUSDT",
            42150.12m,
            0.5m,
            DateTimeOffset.Parse("2023-07-22T10:33:20Z"),
            "binance-style");

        Assert.True(deduplicator.TryAccept(tick));
        Assert.False(deduplicator.TryAccept(tick));
    }
}
