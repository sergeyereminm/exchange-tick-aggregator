namespace ExchangeTickAggregator.Core.Deduplication;

internal readonly record struct TickFingerprint(
    string Source,
    string Ticker,
    DateTimeOffset Timestamp,
    decimal Price,
    decimal Volume)
{
    public static TickFingerprint From(Tick tick) =>
        new(tick.Source, tick.Ticker, tick.Timestamp, tick.Price, tick.Volume);
}
