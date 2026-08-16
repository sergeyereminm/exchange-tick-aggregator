using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using ExchangeTickAggregator.Core;
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
        var app = CreateTickServer(port, closeAfterTicks: 0);
        await app.StartAsync();

        var worker = CreateWorker(out var tickBuffer,
            ("unavailable", GetAvailablePort()),
            ("healthy", port));

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

    [Fact]
    public async Task StartAsync_reconnects_a_dropped_source_without_stopping_the_other()
    {
        var flakyPort = GetAvailablePort();
        var stablePort = GetAvailablePort();
        var flakyConnections = 0;

        var flaky = CreateTickServer(flakyPort, closeAfterTicks: 1, connectionCount: () =>
            Interlocked.Increment(ref flakyConnections));
        var stable = CreateTickServer(stablePort, closeAfterTicks: 0);

        await flaky.StartAsync();
        await stable.StartAsync();

        var worker = CreateWorker(out var tickBuffer,
            ("flaky", flakyPort),
            ("stable", stablePort));

        try
        {
            await worker.StartAsync(CancellationToken.None);

            var ticks = await CollectTicksAsync(
                tickBuffer,
                TimeSpan.FromSeconds(8),
                collected =>
                    collected.Count(tick => tick.Source == "flaky") >= 2 &&
                    collected.Count(tick => tick.Source == "stable") >= 2);

            Assert.True(flakyConnections >= 2, "The dropped source should reconnect.");
            Assert.Contains(ticks, tick => tick.Source == "flaky");
            Assert.Contains(ticks, tick => tick.Source == "stable");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await flaky.StopAsync();
            await stable.StopAsync();
            await flaky.DisposeAsync();
            await stable.DisposeAsync();
        }
    }

    private static WebApplication CreateTickServer(
        int port,
        int closeAfterTicks,
        Func<int>? connectionCount = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/ticks", async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = connectionCount?.Invoke() ?? 1;
            var sentTicks = 0;

            while (socket.State == WebSocketState.Open)
            {
                sentTicks++;
                var message = Encoding.UTF8.GetBytes(
                    $$"""{"s":"BTCUSDT","p":"42150.12","q":"0.5","T":{{1_690_022_000_000 + (connection * 1_000) + sentTicks}}}""");

                await socket.SendAsync(message, WebSocketMessageType.Text, true, context.RequestAborted);

                if (closeAfterTicks > 0 && sentTicks >= closeAfterTicks)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Test disconnect.",
                        context.RequestAborted);
                    return;
                }

                await Task.Delay(50, context.RequestAborted);
            }
        });

        return app;
    }

    private static TickIngestionWorker CreateWorker(
        out BoundedTickBuffer tickBuffer,
        params (string Name, int Port)[] exchanges)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Ingestion:BufferCapacity"] = "50",
            ["Ingestion:DeduplicationWindowSeconds"] = "10",
            ["Ingestion:IdleTimeoutSeconds"] = "15",
            ["Ingestion:ReconnectInitialDelaySeconds"] = "1",
            ["Ingestion:ReconnectMaxDelaySeconds"] = "1"
        };

        for (var i = 0; i < exchanges.Length; i++)
        {
            settings[$"Exchanges:{i}:Name"] = exchanges[i].Name;
            settings[$"Exchanges:{i}:Endpoint"] = $"ws://127.0.0.1:{exchanges[i].Port}/ticks";
            settings[$"Exchanges:{i}:Format"] = "BinanceStyle";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        tickBuffer = new BoundedTickBuffer(capacity: 50);

        return new TickIngestionWorker(
            configuration,
            tickBuffer,
            new TickDeduplicator(TimeSpan.FromSeconds(10)),
            new TickMetrics(),
            NullLogger<TickIngestionWorker>.Instance);
    }

    private static async Task<List<Tick>> CollectTicksAsync(
        BoundedTickBuffer tickBuffer,
        TimeSpan timeout,
        Func<IReadOnlyList<Tick>, bool> isComplete)
    {
        var ticks = new List<Tick>();

        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            while (await tickBuffer.WaitToReadAsync(cancellation.Token))
            {
                while (tickBuffer.TryDequeue(out var tick))
                    ticks.Add(tick);

                if (isComplete(ticks))
                    return ticks;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }

        return ticks;
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
