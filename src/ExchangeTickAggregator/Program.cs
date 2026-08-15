using ExchangeTickAggregator.Core.Deduplication;
using ExchangeTickAggregator.Core.Pipeline;
using ExchangeTickAggregator.Ingestion;

var builder = Host.CreateApplicationBuilder(args);

var ingestionOptions = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>()
    ?? throw new InvalidOperationException("Ingestion configuration is missing.");

builder.Services.AddSingleton(new BoundedTickBuffer(ingestionOptions.BufferCapacity));
builder.Services.AddSingleton(new TickDeduplicator(TimeSpan.FromSeconds(ingestionOptions.DeduplicationWindowSeconds)));
builder.Services.AddHostedService<TickIngestionWorker>();

var host = builder.Build();
host.Run();
