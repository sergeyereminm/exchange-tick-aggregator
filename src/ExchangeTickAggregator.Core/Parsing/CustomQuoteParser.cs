using System.Globalization;
using System.Text.Json;

namespace ExchangeTickAggregator.Core.Parsing;

public sealed class CustomQuoteParser(string source) : IQuoteParser
{
    public Tick Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var ticker = root.GetProperty("ticker").GetString()
            ?? throw new InvalidOperationException("Ticker is missing.");

        var price = decimal.Parse(
            root.GetProperty("last").GetString()!,
            CultureInfo.InvariantCulture);

        var volume = decimal.Parse(
            root.GetProperty("size").GetString()!,
            CultureInfo.InvariantCulture);

        var timestampSeconds = long.Parse(
            root.GetProperty("ts").GetString()!,
            CultureInfo.InvariantCulture);

        return new Tick(
            ticker,
            price,
            volume,
            DateTimeOffset.FromUnixTimeSeconds(timestampSeconds),
            source);
    }
}
