using ExchangeTickAggregator.Core.Parsing;

namespace ExchangeTickAggregator.Tests.Parsing;

public class CoinbaseStyleQuoteParserTests
{
    [Fact]
    public void Parse_maps_coinbase_style_fields_to_tick()
    {
        const string json = """
            {"product_id":"BTC-USD","price":42150.12,"volume":0.5,"time":"2023-07-22T10:33:20.123456Z"}
            """;

        var parser = new CoinbaseStyleQuoteParser("coinbase-style");

        var tick = parser.Parse(json);

        Assert.Equal("BTC-USD", tick.Ticker);
        Assert.Equal(42150.12m, tick.Price);
        Assert.Equal(0.5m, tick.Volume);
        Assert.Equal(DateTimeOffset.Parse("2023-07-22T10:33:20.123456Z"), tick.Timestamp);
        Assert.Equal("coinbase-style", tick.Source);
    }
}
