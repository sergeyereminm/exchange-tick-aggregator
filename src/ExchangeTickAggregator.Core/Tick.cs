namespace ExchangeTickAggregator.Core;

public sealed record Tick(
    string Ticker,
    decimal Price,
    decimal Volume,
    DateTimeOffset Timestamp,
    string Source);
