using ExchangeTickAggregator.Core.Monitoring;
using ExchangeTickAggregator.Core.Pipeline;

namespace ExchangeTickAggregator.Monitoring;

public sealed class TickMetricsLogger(
    TickMetrics metrics,
    BoundedTickBuffer tickBuffer,
    ILogger<TickMetricsLogger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = metrics.GetSnapshot();

            logger.LogInformation(
                "Tick metrics: received {ReceivedTickCount}, malformed {MalformedTickCount}, duplicate {DuplicateTickCount}, buffered {BufferedTickCount}, persisted {PersistedTickCount}, dropped {DroppedTickCount}, queue depth {QueueDepth}",
                snapshot.ReceivedTickCount,
                snapshot.MalformedTickCount,
                snapshot.DuplicateTickCount,
                snapshot.BufferedTickCount,
                snapshot.PersistedTickCount,
                snapshot.DroppedTickCount,
                tickBuffer.Count);
        }
    }
}
