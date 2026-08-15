namespace ExchangeTickAggregator.Ingestion;

public sealed class ExchangeOptions
{
    public string Name { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;
}
