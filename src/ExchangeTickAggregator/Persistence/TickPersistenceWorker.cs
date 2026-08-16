using ExchangeTickAggregator.Core.Persistence;
using ExchangeTickAggregator.Core.Pipeline;

namespace ExchangeTickAggregator.Persistence;

public sealed class TickPersistenceWorker(
    BoundedTickBuffer tickBuffer,
    BatchTickWriter batchWriter,
    IConfiguration configuration,
    ILogger<TickPersistenceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var persistence = configuration.GetSection("Persistence").Get<PersistenceOptions>()
            ?? throw new InvalidOperationException("Persistence configuration is missing.");

        var flushInterval = TimeSpan.FromMilliseconds(persistence.FlushIntervalMilliseconds);
        var drainTimeout = TimeSpan.FromMilliseconds(persistence.DrainTimeoutMilliseconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var flushTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushTimeout.CancelAfter(flushInterval);

                try
                {
                    if (!await tickBuffer.WaitToReadAsync(flushTimeout.Token))
                        break;

                    while (tickBuffer.TryDequeue(out var tick))
                        await batchWriter.WriteAsync(tick, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    await batchWriter.FlushAsync(stoppingToken);
                }
            }
        }
        finally
        {
            try
            {
                using var shutdownTimeout = new CancellationTokenSource(drainTimeout);
                await TickChannelDrain.DrainAsync(tickBuffer, batchWriter, shutdownTimeout.Token);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to drain buffered ticks during shutdown");
            }
        }
    }
}
