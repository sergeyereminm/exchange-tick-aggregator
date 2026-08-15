using System.Net.WebSockets;
using System.Text;
using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Deduplication;
using ExchangeTickAggregator.Core.Monitoring;
using ExchangeTickAggregator.Core.Parsing;
using ExchangeTickAggregator.Core.Pipeline;
using ExchangeTickAggregator.Core.Reconnect;

namespace ExchangeTickAggregator.Ingestion;

public sealed class TickIngestionWorker(
    IConfiguration configuration,
    BoundedTickBuffer tickBuffer,
    TickDeduplicator deduplicator,
    TickMetrics metrics,
    ILogger<TickIngestionWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ingestion = configuration.GetSection("Ingestion").Get<IngestionOptions>()
            ?? throw new InvalidOperationException("Ingestion configuration is missing.");

        var exchanges = configuration.GetSection("Exchanges").Get<ExchangeOptions[]>()
            ?? throw new InvalidOperationException("Exchange configuration is missing.");

        if (exchanges.Length == 0)
            throw new InvalidOperationException("At least one exchange must be configured.");

        return Task.WhenAll(exchanges.Select(exchange =>
            IngestExchangeAsync(exchange, ingestion, stoppingToken)));
    }

    private async Task IngestExchangeAsync(
        ExchangeOptions exchange,
        IngestionOptions ingestion,
        CancellationToken stoppingToken)
    {
        var parser = QuoteParserFactory.Create(exchange);
        var backoff = new ExponentialBackoffPolicy(
            TimeSpan.FromSeconds(ingestion.ReconnectInitialDelaySeconds),
            TimeSpan.FromSeconds(ingestion.ReconnectMaxDelaySeconds),
            factor: 2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri(exchange.Endpoint), stoppingToken);
                logger.LogInformation("Connected to exchange {Exchange}", exchange.Name);

                backoff.Reset();
                await ReceiveTicksAsync(socket, parser, exchange.Name, ingestion, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Exchange {Exchange} connection ended", exchange.Name);
            }

            var delay = backoff.NextDelay();
            logger.LogInformation(
                "Reconnecting to exchange {Exchange} after {Delay}",
                exchange.Name,
                delay);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ReceiveTicksAsync(
        ClientWebSocket socket,
        IQuoteParser parser,
        string exchangeName,
        IngestionOptions ingestion,
        CancellationToken stoppingToken)
    {
        var watchdog = new IdleTimeoutWatchdog(TimeSpan.FromSeconds(ingestion.IdleTimeoutSeconds));
        var buffer = new byte[4096];

        while (socket.State == WebSocketState.Open)
        {
            using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            idleTimeout.CancelAfter(TimeSpan.FromSeconds(ingestion.IdleTimeoutSeconds));

            WebSocketReceiveResult result;

            try
            {
                result = await socket.ReceiveAsync(buffer, idleTimeout.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Exchange '{exchangeName}' exceeded its idle timeout.");
            }

            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException($"Exchange '{exchangeName}' closed the connection.");

            if (!result.EndOfMessage)
                throw new InvalidOperationException(
                    $"Exchange '{exchangeName}' sent a fragmented message larger than {buffer.Length} bytes.");

            var receivedAt = DateTimeOffset.UtcNow;
            watchdog.NotifyActivity(receivedAt);
            metrics.RecordReceived();

            if (watchdog.HasTimedOut(receivedAt))
                throw new TimeoutException($"Exchange '{exchangeName}' exceeded its idle timeout.");

            Tick tick;

            try
            {
                tick = parser.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            catch (Exception exception)
            {
                metrics.RecordMalformed();
                logger.LogWarning(exception, "Discarded malformed tick from exchange {Exchange}", exchangeName);
                continue;
            }

            if (!deduplicator.TryAccept(tick))
            {
                metrics.RecordDuplicate();
                continue;
            }

            await tickBuffer.EnqueueAsync(tick, stoppingToken);
            metrics.RecordBuffered();
        }
    }
}
