namespace ExchangeTickAggregator.Core.Monitoring;

public sealed class TickMetrics
{
    private long _receivedTickCount;
    private long _malformedTickCount;
    private long _duplicateTickCount;
    private long _bufferedTickCount;
    private long _persistedTickCount;
    private long _droppedTickCount;

    public void RecordReceived() => Interlocked.Increment(ref _receivedTickCount);

    public void RecordMalformed() => Interlocked.Increment(ref _malformedTickCount);

    public void RecordDuplicate() => Interlocked.Increment(ref _duplicateTickCount);

    public void RecordBuffered() => Interlocked.Increment(ref _bufferedTickCount);

    public void RecordPersisted(int tickCount) => Interlocked.Add(ref _persistedTickCount, tickCount);

    public void RecordDropped(int tickCount) => Interlocked.Add(ref _droppedTickCount, tickCount);

    public TickMetricsSnapshot GetSnapshot() =>
        new(
            Interlocked.Read(ref _receivedTickCount),
            Interlocked.Read(ref _malformedTickCount),
            Interlocked.Read(ref _duplicateTickCount),
            Interlocked.Read(ref _bufferedTickCount),
            Interlocked.Read(ref _persistedTickCount),
            Interlocked.Read(ref _droppedTickCount));
}

public sealed record TickMetricsSnapshot(
    long ReceivedTickCount,
    long MalformedTickCount,
    long DuplicateTickCount,
    long BufferedTickCount,
    long PersistedTickCount,
    long DroppedTickCount);
