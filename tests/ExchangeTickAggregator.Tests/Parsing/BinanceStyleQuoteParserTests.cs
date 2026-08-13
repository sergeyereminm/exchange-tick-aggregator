using ExchangeTickAggregator.Core.Parsing;

namespace ExchangeTickAggregator.Tests.Parsing;

public class BinanceStyleQuoteParserTests
{
    [Fact]
    public void Parse_maps_binance_style_fields_to_tick()
    {
        const string json = """{"s":"BTCUSDT","p":"42150.12","q":"0.5","T":1690000000123}""";

        var parser = new BinanceStyleQuoteParser("binance-style");

        var tick = parser.Parse(json);

        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Equal(42150.12m, tick.Price);
        Assert.Equal(0.5m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1690000000123), tick.Timestamp);
        Assert.Equal("binance-style", tick.Source);
    }
}
