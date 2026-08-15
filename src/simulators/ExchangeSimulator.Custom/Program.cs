using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var tickers = new[] { "BTC-USD", "ETH-USD", "SOL-USD" };

app.UseWebSockets();

app.Map("/ticks", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var tickInterval = TimeSpan.FromMilliseconds(
        builder.Configuration.GetValue<int>("Simulator:TickIntervalMilliseconds", 5));

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var timer = new PeriodicTimer(tickInterval);

    while (socket.State == WebSocketState.Open &&
           await timer.WaitForNextTickAsync(context.RequestAborted))
    {
        var price = 100m + ((decimal)Random.Shared.NextDouble() * 49_900m);
        var size = 0.01m + ((decimal)Random.Shared.NextDouble() * 4.99m);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            ticker = tickers[Random.Shared.Next(tickers.Length)],
            last = price.ToString("F2", CultureInfo.InvariantCulture),
            size = size.ToString("F4", CultureInfo.InvariantCulture),
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        });

        await socket.SendAsync(payload, WebSocketMessageType.Text, true, context.RequestAborted);
    }
});

app.Run();
