using System.Globalization;
using System.Text.Json;

namespace ExchangeTickAggregator.Core.Parsing;

public sealed class CoinbaseStyleQuoteParser(string source) : IQuoteParser
{
    public Tick Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var ticker = root.GetProperty("product_id").GetString()
            ?? throw new InvalidOperationException("Ticker is missing.");

        var price = root.GetProperty("price").GetDecimal();
        var volume = root.GetProperty("volume").GetDecimal();

        var timestamp = DateTimeOffset.Parse(
            root.GetProperty("time").GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return new Tick(ticker, price, volume, timestamp, source);
    }
}
