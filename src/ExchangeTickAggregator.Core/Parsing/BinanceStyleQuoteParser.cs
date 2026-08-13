using System.Globalization;
using System.Text.Json;

namespace ExchangeTickAggregator.Core.Parsing;

public sealed class BinanceStyleQuoteParser(string source) : IQuoteParser
{
    public Tick Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var ticker = root.GetProperty("s").GetString()
            ?? throw new InvalidOperationException("Ticker is missing.");

        var price = decimal.Parse(
            root.GetProperty("p").GetString()!,
            CultureInfo.InvariantCulture);

        var volume = decimal.Parse(
            root.GetProperty("q").GetString()!,
            CultureInfo.InvariantCulture);

        var timestampMilliseconds = root.GetProperty("T").GetInt64();

        return new Tick(
            ticker,
            price,
            volume,
            DateTimeOffset.FromUnixTimeMilliseconds(timestampMilliseconds),
            source);
    }
}
