using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using ExchangeTickAggregator.Core.Deduplication;
using ExchangeTickAggregator.Core.Monitoring;
using ExchangeTickAggregator.Core.Pipeline;
using ExchangeTickAggregator.Ingestion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExchangeTickAggregator.Tests.Ingestion;

public class TickIngestionWorkerTests
{
    [Fact]
    public async Task StartAsync_keeps_healthy_source_ingesting_when_another_source_is_unavailable()
    {
        var port = GetAvailablePort();
        var app = CreateTickServer(port);
        await app.StartAsync();

        var worker = CreateWorker(port, GetAvailablePort(), out var tickBuffer);

        try
        {
            await worker.StartAsync(CancellationToken.None);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.True(await tickBuffer.WaitToReadAsync(timeout.Token));
            Assert.True(tickBuffer.TryDequeue(out var tick));
            Assert.Equal("healthy", tick.Source);
            Assert.Equal("BTCUSDT", tick.Ticker);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static WebApplication CreateTickServer(int port)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/ticks", async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var message = Encoding.UTF8.GetBytes(
                """{"s":"BTCUSDT","p":"42150.12","q":"0.5","T":1690022000000}""");

            await socket.SendAsync(message, WebSocketMessageType.Text, true, context.RequestAborted);
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });

        return app;
    }

    private static TickIngestionWorker CreateWorker(
        int healthyPort,
        int unavailablePort,
        out BoundedTickBuffer tickBuffer)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ingestion:BufferCapacity"] = "10",
                ["Ingestion:DeduplicationWindowSeconds"] = "10",
                ["Ingestion:IdleTimeoutSeconds"] = "15",
                ["Ingestion:ReconnectInitialDelaySeconds"] = "1",
                ["Ingestion:ReconnectMaxDelaySeconds"] = "1",
                ["Exchanges:0:Name"] = "unavailable",
                ["Exchanges:0:Endpoint"] = $"ws://127.0.0.1:{unavailablePort}/ticks",
                ["Exchanges:0:Format"] = "BinanceStyle",
                ["Exchanges:1:Name"] = "healthy",
                ["Exchanges:1:Endpoint"] = $"ws://127.0.0.1:{healthyPort}/ticks",
                ["Exchanges:1:Format"] = "BinanceStyle"
            })
            .Build();

        tickBuffer = new BoundedTickBuffer(capacity: 10);

        return new TickIngestionWorker(
            configuration,
            tickBuffer,
            new TickDeduplicator(TimeSpan.FromSeconds(10)),
            new TickMetrics(),
            NullLogger<TickIngestionWorker>.Instance);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
