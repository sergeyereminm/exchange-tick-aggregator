namespace ExchangeTickAggregator.Core.Parsing;

public interface IQuoteParser
{
    Tick Parse(string json);
}
