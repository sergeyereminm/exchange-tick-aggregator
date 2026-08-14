namespace ExchangeTickAggregator.Core.Persistence;

public sealed class BatchTickWriter
{
    private readonly ITickBatchSink _sink;
    private readonly int _batchSize;
    private readonly List<Tick> _pendingTicks = [];

    public BatchTickWriter(ITickBatchSink sink, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        _sink = sink;
        _batchSize = batchSize;
    }

    public async Task WriteAsync(Tick tick, CancellationToken cancellationToken = default)
    {
        _pendingTicks.Add(tick);

        if (_pendingTicks.Count < _batchSize)
            return;

        await FlushAsync(cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingTicks.Count == 0)
            return;

        var batch = _pendingTicks.ToArray();
        _pendingTicks.Clear();

        await _sink.WriteAsync(batch, cancellationToken);
    }
}
