using ExchangeTickAggregator.Core.Pipeline;

namespace ExchangeTickAggregator.Core.Persistence;

public static class TickChannelDrain
{
    public static async Task DrainAsync(
        BoundedTickBuffer buffer,
        BatchTickWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(writer);

        while (buffer.TryDequeue(out var tick))
            await writer.WriteAsync(tick, cancellationToken);

        await writer.FlushAsync(cancellationToken);
    }
}
