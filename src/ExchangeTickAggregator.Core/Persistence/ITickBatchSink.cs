namespace ExchangeTickAggregator.Core.Persistence;

public interface ITickBatchSink
{
    Task WriteAsync(IReadOnlyList<Tick> ticks, CancellationToken cancellationToken);
}
