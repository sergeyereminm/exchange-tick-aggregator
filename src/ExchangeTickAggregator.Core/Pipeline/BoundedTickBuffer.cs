using System.Threading.Channels;

namespace ExchangeTickAggregator.Core.Pipeline;

public sealed class BoundedTickBuffer
{
    private readonly Channel<Tick> _channel;

    public BoundedTickBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);

        _channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int Count => _channel.Reader.Count;

    public bool TryEnqueue(Tick tick) => _channel.Writer.TryWrite(tick);
}
