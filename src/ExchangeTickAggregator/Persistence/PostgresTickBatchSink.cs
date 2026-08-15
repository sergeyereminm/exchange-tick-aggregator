using ExchangeTickAggregator.Core;
using ExchangeTickAggregator.Core.Persistence;
using Npgsql;

namespace ExchangeTickAggregator.Persistence;

public sealed class PostgresTickBatchSink(NpgsqlDataSource dataSource) : ITickBatchSink
{
    public async Task WriteAsync(IReadOnlyList<Tick> ticks, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var batch = new NpgsqlBatch(connection);

        foreach (var tick in ticks)
        {
            var command = new NpgsqlBatchCommand(
                """
                INSERT INTO ticks (ticker, price, volume, occurred_at, source)
                VALUES ($1, $2, $3, $4, $5)
                """);

            command.Parameters.AddWithValue(tick.Ticker);
            command.Parameters.AddWithValue(tick.Price);
            command.Parameters.AddWithValue(tick.Volume);
            command.Parameters.AddWithValue(tick.Timestamp);
            command.Parameters.AddWithValue(tick.Source);

            batch.BatchCommands.Add(command);
        }

        await batch.ExecuteNonQueryAsync(cancellationToken);
    }
}
