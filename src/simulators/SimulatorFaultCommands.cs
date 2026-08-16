internal sealed class SimulatorFaultCommands
{
    private int _disconnectVersion;
    private int _duplicateVersion;

    public int DisconnectVersion => Volatile.Read(ref _disconnectVersion);

    public int DuplicateVersion => Volatile.Read(ref _duplicateVersion);

    public void Disconnect() => Interlocked.Increment(ref _disconnectVersion);

    public void Duplicate() => Interlocked.Increment(ref _duplicateVersion);
}

internal static class SimulatorFaultEndpoints
{
    public static void MapFaultCommands(this WebApplication app, SimulatorFaultCommands faults)
    {
        app.MapGet("/fault", () => Results.Ok(new
        {
            commands = new
            {
                disconnect = "POST /fault/disconnect",
                duplicate = "POST /fault/duplicate"
            }
        }));

        app.MapPost("/fault/disconnect", () =>
        {
            faults.Disconnect();
            return Results.Ok(new { command = "disconnect" });
        });

        app.MapPost("/fault/duplicate", () =>
        {
            faults.Duplicate();
            return Results.Ok(new { command = "duplicate" });
        });
    }
}
