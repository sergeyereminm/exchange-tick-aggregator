using System.Net.WebSockets;
using System.Globalization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var tickers = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" };
var faults = new SimulatorFaultCommands();

app.UseWebSockets();
app.MapFaultCommands(faults);

app.Map("/ticks", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var tickInterval = TimeSpan.FromMilliseconds(
        builder.Configuration.GetValue<int>("Simulator:TickIntervalMilliseconds", 5));
    var duplicateEveryTicks = builder.Configuration.GetValue<int>("Simulator:DuplicateEveryTicks");
    var disconnectAfterTicks = builder.Configuration.GetValue<int>("Simulator:DisconnectAfterTicks");

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var timer = new PeriodicTimer(tickInterval);
    var sentTicks = 0;
    var seenDisconnectVersion = faults.DisconnectVersion;
    var seenDuplicateVersion = faults.DuplicateVersion;

    while (socket.State == WebSocketState.Open &&
           await timer.WaitForNextTickAsync(context.RequestAborted))
    {
        var price = 100m + ((decimal)Random.Shared.NextDouble() * 49_900m);
        var volume = 0.01m + ((decimal)Random.Shared.NextDouble() * 4.99m);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            s = tickers[Random.Shared.Next(tickers.Length)],
            p = price.ToString("F2", CultureInfo.InvariantCulture),
            q = volume.ToString("F4", CultureInfo.InvariantCulture),
            T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        await socket.SendAsync(payload, WebSocketMessageType.Text, true, context.RequestAborted);
        sentTicks++;

        var duplicateVersion = faults.DuplicateVersion;
        if (duplicateVersion != seenDuplicateVersion ||
            (duplicateEveryTicks > 0 && sentTicks % duplicateEveryTicks == 0))
        {
            seenDuplicateVersion = duplicateVersion;
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, context.RequestAborted);
        }

        var disconnectVersion = faults.DisconnectVersion;
        if (disconnectVersion != seenDisconnectVersion ||
            (disconnectAfterTicks > 0 && sentTicks >= disconnectAfterTicks))
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "Simulator disconnect.",
                context.RequestAborted);
            break;
        }
    }
});

app.Run();
