using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Persistence;
using ExchangeTickAggregator.Core.Pipeline;

namespace ExchangeTickAggregator.Tests.Persistence;

public class TickChannelDrainTests
{
    [Fact]
    public async Task DrainAsync_writes_remaining_buffered_ticks_and_flushes_pending_batch()
    {
        var sink = new RecordingTickBatchSink();
        var writer = new BatchTickWriter(sink, batchSize: 10);
        var buffer = new BoundedTickBuffer(capacity: 10);
        var bufferedTick = CreateTick("ETHUSDT");
        var pendingTick = CreateTick("BTCUSDT");

        await writer.WriteAsync(pendingTick);
        Assert.True(buffer.TryEnqueue(bufferedTick));

        await TickChannelDrain.DrainAsync(buffer, writer, CancellationToken.None);

        var batch = Assert.Single(sink.Batches);
        Assert.Equal([pendingTick, bufferedTick], batch);
        Assert.Equal(0, buffer.Count);
    }

    private static Tick CreateTick(string ticker) =>
        new(
            ticker,
            42150.12m,
            0.5m,
            DateTimeOffset.Parse("2023-07-22T10:33:20Z"),
            "binance-style");

    private sealed class RecordingTickBatchSink : ITickBatchSink
    {
        public List<IReadOnlyList<Tick>> Batches { get; } = [];

        public Task WriteAsync(IReadOnlyList<Tick> ticks, CancellationToken cancellationToken)
        {
            Batches.Add(ticks);
            return Task.CompletedTask;
        }
    }
}
