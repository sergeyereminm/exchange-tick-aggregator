using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Pipeline;

namespace ExchangeTickAggregator.Tests.Pipeline;

public class BoundedTickBufferTests
{
    [Fact]
    public void TryEnqueue_rejects_ticks_when_capacity_is_reached()
    {
        var buffer = new BoundedTickBuffer(capacity: 2);
        var tick = new Tick(
            "BTCUSDT",
            42150.12m,
            0.5m,
            DateTimeOffset.Parse("2023-07-22T10:33:20Z"),
            "binance-style");

        Assert.True(buffer.TryEnqueue(tick));
        Assert.True(buffer.TryEnqueue(tick));
        Assert.False(buffer.TryEnqueue(tick));
        Assert.Equal(2, buffer.Count);
    }
}
