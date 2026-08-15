namespace ExchangeTickAggregator.Ingestion;

public sealed class IngestionOptions
{
    public int BufferCapacity { get; init; }

    public int DeduplicationWindowSeconds { get; init; }

    public int IdleTimeoutSeconds { get; init; }

    public int ReconnectInitialDelaySeconds { get; init; }

    public int ReconnectMaxDelaySeconds { get; init; }
}
