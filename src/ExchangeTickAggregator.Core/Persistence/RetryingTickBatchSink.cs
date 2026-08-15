namespace ExchangeTickAggregator.Core.Persistence;

public sealed class RetryingTickBatchSink : ITickBatchSink
{
    private readonly ITickBatchSink _inner;
    private readonly int _maxAttempts;
    private readonly Action<int>? _onBatchWritten;
    private readonly Action<Exception, int>? _onBatchDropped;
    private long _droppedTickCount;

    public RetryingTickBatchSink(
        ITickBatchSink inner,
        int maxAttempts,
        Action<int>? onBatchWritten = null,
        Action<Exception, int>? onBatchDropped = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAttempts, 0);

        _inner = inner;
        _maxAttempts = maxAttempts;
        _onBatchWritten = onBatchWritten;
        _onBatchDropped = onBatchDropped;
    }

    public long DroppedTickCount => Interlocked.Read(ref _droppedTickCount);

    public async Task WriteAsync(IReadOnlyList<Tick> ticks, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                await _inner.WriteAsync(ticks, cancellationToken);
                _onBatchWritten?.Invoke(ticks.Count);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt == _maxAttempts)
                {
                    Interlocked.Add(ref _droppedTickCount, ticks.Count);
                    _onBatchDropped?.Invoke(exception, ticks.Count);
                }
            }
        }
    }
}
