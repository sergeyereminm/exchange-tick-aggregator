using ExchangeTickAggregator.Core.Parsing;

namespace ExchangeTickAggregator.Ingestion;

public static class QuoteParserFactory
{
    public static IQuoteParser Create(ExchangeOptions exchange) =>
        exchange.Format switch
        {
            "BinanceStyle" => new BinanceStyleQuoteParser(exchange.Name),
            "CoinbaseStyle" => new CoinbaseStyleQuoteParser(exchange.Name),
            "Custom" => new CustomQuoteParser(exchange.Name),
            _ => throw new InvalidOperationException(
                $"Exchange '{exchange.Name}' has unsupported format '{exchange.Format}'.")
        };
}
