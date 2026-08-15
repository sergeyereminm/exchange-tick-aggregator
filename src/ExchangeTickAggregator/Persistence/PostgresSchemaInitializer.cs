using Npgsql;

namespace ExchangeTickAggregator.Persistence;

public sealed class PostgresSchemaInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            CREATE TABLE IF NOT EXISTS ticks (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                ticker TEXT NOT NULL,
                price NUMERIC(20, 8) NOT NULL,
                volume NUMERIC(20, 8) NOT NULL,
                occurred_at TIMESTAMPTZ NOT NULL,
                source TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_ticks_occurred_at ON ticks (occurred_at);
            """);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
