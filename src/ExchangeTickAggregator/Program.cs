using ExchangeTickAggregator.Core.Deduplication;
using ExchangeTickAggregator.Core.Monitoring;
using ExchangeTickAggregator.Core.Pipeline;
using ExchangeTickAggregator.Core.Persistence;
using ExchangeTickAggregator.Ingestion;
using ExchangeTickAggregator.Monitoring;
using ExchangeTickAggregator.Persistence;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var ingestionOptions = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>()
    ?? throw new InvalidOperationException("Ingestion configuration is missing.");
var persistenceOptions = builder.Configuration.GetSection("Persistence").Get<PersistenceOptions>()
    ?? throw new InvalidOperationException("Persistence configuration is missing.");

builder.Services.AddSingleton(new BoundedTickBuffer(ingestionOptions.BufferCapacity));
builder.Services.AddSingleton(new TickDeduplicator(TimeSpan.FromSeconds(ingestionOptions.DeduplicationWindowSeconds)));
builder.Services.AddSingleton<TickMetrics>();
builder.Services.AddSingleton(NpgsqlDataSource.Create(persistenceOptions.ConnectionString));
builder.Services.AddSingleton<PostgresTickBatchSink>();
builder.Services.AddSingleton<ITickBatchSink>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<RetryingTickBatchSink>>();

    return new RetryingTickBatchSink(
        serviceProvider.GetRequiredService<PostgresTickBatchSink>(),
        persistenceOptions.MaxWriteAttempts,
        serviceProvider.GetRequiredService<TickMetrics>().RecordPersisted,
        (exception, tickCount) =>
        {
            serviceProvider.GetRequiredService<TickMetrics>().RecordDropped(tickCount);
            logger.LogError(exception, "Dropped {TickCount} ticks after database write retries", tickCount);
        },
        TimeSpan.FromMilliseconds(persistenceOptions.WriteRetryDelayMilliseconds));
});
builder.Services.AddSingleton(serviceProvider =>
    new BatchTickWriter(
        serviceProvider.GetRequiredService<ITickBatchSink>(),
        persistenceOptions.BatchSize));
builder.Services.AddHostedService<PostgresSchemaInitializer>();
builder.Services.AddHostedService<TickPersistenceWorker>();
builder.Services.AddHostedService<TickIngestionWorker>();
builder.Services.AddHostedService<TickMetricsLogger>();

var host = builder.Build();
host.Run();
