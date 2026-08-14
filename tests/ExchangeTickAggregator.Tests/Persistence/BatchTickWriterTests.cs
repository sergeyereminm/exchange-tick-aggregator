using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Persistence;

namespace ExchangeTickAggregator.Tests.Persistence;

public class BatchTickWriterTests
{
    [Fact]
    public async Task WriteAsync_flushes_ticks_when_batch_size_is_reached()
    {
        var sink = new RecordingTickBatchSink();
        var writer = new BatchTickWriter(sink, batchSize: 2);
        var firstTick = CreateTick("BTCUSDT");
        var secondTick = CreateTick("ETHUSDT");

        await writer.WriteAsync(firstTick);
        await writer.WriteAsync(secondTick);

        var batch = Assert.Single(sink.Batches);
        Assert.Equal([firstTick, secondTick], batch);
    }

    [Fact]
    public async Task FlushAsync_writes_pending_ticks_before_shutdown()
    {
        var sink = new RecordingTickBatchSink();
        var writer = new BatchTickWriter(sink, batchSize: 2);
        var tick = CreateTick("BTCUSDT");

        await writer.WriteAsync(tick);
        await writer.FlushAsync();

        var batch = Assert.Single(sink.Batches);
        Assert.Equal([tick], batch);
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
