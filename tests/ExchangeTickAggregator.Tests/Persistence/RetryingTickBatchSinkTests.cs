using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Persistence;

namespace ExchangeTickAggregator.Tests.Persistence;

public class RetryingTickBatchSinkTests
{
    [Fact]
    public async Task WriteAsync_retries_failed_batch_and_counts_dropped_ticks_after_attempt_limit()
    {
        var failingSink = new FailingTickBatchSink();
        var sink = new RetryingTickBatchSink(failingSink, maxAttempts: 3, retryDelay: TimeSpan.Zero);
        var batch = new[] { CreateTick("BTCUSDT"), CreateTick("ETHUSDT") };

        await sink.WriteAsync(batch, CancellationToken.None);

        Assert.Equal(3, failingSink.AttemptCount);
        Assert.Equal(2, sink.DroppedTickCount);
    }

    private static Tick CreateTick(string ticker) =>
        new(
            ticker,
            42150.12m,
            0.5m,
            DateTimeOffset.Parse("2023-07-22T10:33:20Z"),
            "binance-style");

    private sealed class FailingTickBatchSink : ITickBatchSink
    {
        public int AttemptCount { get; private set; }

        public Task WriteAsync(IReadOnlyList<Tick> ticks, CancellationToken cancellationToken)
        {
            AttemptCount++;
            throw new InvalidOperationException("Database is unavailable.");
        }
    }
}
