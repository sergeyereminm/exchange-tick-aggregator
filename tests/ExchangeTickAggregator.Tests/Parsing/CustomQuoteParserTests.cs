using ExchangeTickAggregator.Core.Parsing;

namespace ExchangeTickAggregator.Tests.Parsing;

public class CustomQuoteParserTests
{
    [Fact]
    public void Parse_maps_custom_fields_to_tick()
    {
        const string json = """{"ticker":"ETH-USD","last":"2500.55","size":"1.25","ts":"1690000000"}""";

        var parser = new CustomQuoteParser("custom");

        var tick = parser.Parse(json);

        Assert.Equal("ETH-USD", tick.Ticker);
        Assert.Equal(2500.55m, tick.Price);
        Assert.Equal(1.25m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1690000000), tick.Timestamp);
        Assert.Equal("custom", tick.Source);
    }
}
