namespace ExchangeTickAggregator.Persistence;

public sealed class PersistenceOptions
{
    public string ConnectionString { get; init; } = string.Empty;

    public int BatchSize { get; init; }

    public int FlushIntervalMilliseconds { get; init; }

    public int MaxWriteAttempts { get; init; }
}
