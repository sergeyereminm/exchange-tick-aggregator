# Exchange tick aggregator

Real-time aggregation of exchange quotes from three WebSocket simulators into PostgreSQL.

Russian version: [README_ru.md](README_ru.md).

The aggregator connects to all sources in parallel, normalizes payloads, deduplicates concurrent ticks, applies backpressure through a bounded channel, and writes batched rows to Postgres. Reconnect, idle timeout, and database retries are part of the design, not bolted on later.

## Requirements

- .NET 8 SDK
- Docker Desktop — for the full stack
- Optional: `psql` or any Postgres client to inspect stored ticks

## Quick start (Docker Compose)

From this repository root:

```bash
docker compose up --build
```

This starts:

| Service | Role | Host access |
|---|---|---|
| `postgres` | Tick storage | `127.0.0.1:15432` |
| `binance-style` | WebSocket simulator | `127.0.0.1:5001` |
| `coinbase-style` | WebSocket simulator | `127.0.0.1:5002` |
| `custom` | WebSocket simulator | `127.0.0.1:5003` |
| `aggregator` | Ingestion + persistence | no published ports; use `docker compose logs aggregator` |

Postgres is mapped to host port **15432** so it does not clash with a local Postgres on 5432.

Compose enables fault injection on the simulators by default (`DuplicateEveryTicks=50`, `DisconnectAfterTicks=200`) so reconnect and deduplication run continuously.

Inspect stored ticks:

```bash
docker compose exec postgres psql -U aggregator -d ticks -c "SELECT source, COUNT(*) FROM ticks GROUP BY source ORDER BY source;"
```

Aggregator logs (connect, disconnect, metrics):

```bash
docker compose logs -f aggregator
```

### Watch a simulator in the browser

The address bar will not open `ws://...`. Use this instead:

1. In Chrome, open `http://127.0.0.1:5001/ticks`. A **400** response is expected: this is a WebSocket endpoint, not HTML.
2. Press `F12` and open the Console tab.
3. Paste:

```javascript
const ws = new WebSocket("ws://127.0.0.1:5001/ticks");
ws.onopen = () => console.log("connected");
ws.onmessage = (e) => console.log(e.data);
ws.onerror = (e) => console.error(e);
ws.onclose = (e) => console.log("closed", e.code, e.reason);
```

You should see JSON such as `{"s":"BTCUSDT","p":"123.45","q":"0.12","T":...}`. If the console reports mixed content, open `http://127.0.0.1:5001/ticks` or `about:blank` first, then paste the snippet again.

In Compose the simulator closes the socket after about 200 ticks — the console will log `closed`. This snippet does not reconnect; the aggregator reconnects on its own.

Stop the client:

```javascript
ws.close();
```

The same steps work on ports `5002` and `5003`; the JSON shape differs.

Stop and remove volumes:

```bash
docker compose down -v
```

## Local run (aggregator on the host)

Useful when iterating on the worker while the database and/or simulators stay in Docker.

1. Start Postgres (and optionally the simulators):

```bash
docker compose up --build postgres binance-style coinbase-style custom
```

2. Run the aggregator against host-mapped ports (`appsettings.json` already points at `127.0.0.1:15432` and `ws://localhost:5001|5002|5003`):

```bash
dotnet run --project src/ExchangeTickAggregator
```

3. Run simulators individually if you are not using Compose:

```bash
dotnet run --project src/simulators/ExchangeSimulator.BinanceStyle
dotnet run --project src/simulators/ExchangeSimulator.CoinbaseStyle
dotnet run --project src/simulators/ExchangeSimulator.Custom
```

## Tests

```bash
dotnet test
```

The suite covers failure paths as well as happy paths:

- concurrent deduplication
- bounded channel capacity
- batch flush, write retries, and dropped-tick counting
- reconnect backoff and idle timeout
- isolated ingestion when one exchange is unavailable

## Architecture

```
Simulators (WS) ──► TickIngestionWorker (per exchange)
                         │ parse → dedup → metrics
                         ▼
                   BoundedTickBuffer (Channel)
                         │
                         ▼
                 TickPersistenceWorker
                         │ batch write + retry
                         ▼
                      PostgreSQL
```

Core policies live in `ExchangeTickAggregator.Core` and can be unit-tested without the host. The worker project wires WebSockets, Npgsql, configuration, and `IHostedService` components.

### Adding an exchange

1. Implement `IQuoteParser` for the new payload shape.
2. Register the format in `QuoteParserFactory`.
3. Add an `Exchanges` entry (name, endpoint, format).

No changes to the ingestion loop or persistence path are required.

## Engineering decisions

### Normalization

Each simulator uses a deliberately different payload:

| Source | Fields | Price type | Timestamp format |
|---|---|---|---|
| Binance-style | `s` / `p` / `q` / `T` | string | Unix ms |
| Coinbase-style | `product_id` / `price` / `volume` / `time` | number | ISO-8601 |
| Custom | `ticker` / `last` / `size` / `ts` | number | Unix seconds as string |

All parsers map to one internal `Tick` (`Ticker`, `Price`, `Volume`, `Timestamp`, `Source`).

### Deduplication

- **Key:** `(Source, Ticker, Timestamp, Price, Volume)`
- **Window:** 10 seconds (`Ingestion:DeduplicationWindowSeconds`)
- **Implementation:** `ConcurrentDictionary` with compare-and-swap so concurrent accept/reject from multiple exchanges stays correct

Trade-off: the window is in-memory and process-local. Expired fingerprints are removed at most once per window, so memory stays bounded by recent unique ticks without scanning the dictionary on every accept. After a restart, the same tick can be stored again. A durable dedup store would remove that risk at the cost of latency and operational complexity.

### Queue and backpressure

Accepted ticks go into a bounded `Channel<T>` (`Ingestion:BufferCapacity`, default 10 000) with `FullMode = Wait`. When persistence falls behind, ingestion loops wait for free capacity instead of dropping ticks. Queue depth is written to the log with the other counters.

Trade-off: short Postgres slowdowns do not lose ticks. If the database stays down longer than the write-retry budget, ingestion can stall — see [Persistence](#persistence).

### Persistence

- Batch size 500 or flush every 1 s (`Persistence:BatchSize` / `FlushIntervalMilliseconds`)
- Multi-row `INSERT` via Npgsql
- Schema is created on startup (`CREATE TABLE IF NOT EXISTS ticks ...`)

On write failure the batch is retried up to `MaxWriteAttempts` (default 3), with `WriteRetryDelayMilliseconds` (default 100 ms) between attempts. If all attempts fail, the batch is dropped, `DroppedTickCount` is incremented, and an error is logged. Silent loss is intentionally avoided: either the write succeeds or the drop is visible in logs and metrics. A failed flush keeps the pending batch in memory so the next attempt can retry the same ticks.

Trade-off: after retries are exhausted, ticks are discarded rather than written elsewhere. A durable outbox would survive longer outages; this solution keeps the failure mode explicit and memory-bounded.

### Reconnect and idle timeout

Each exchange runs in its own loop:

- exponential backoff between reconnects (1 s → 30 s), with jitter so sources do not reconnect in lockstep
- idle timeout (15 s) closes a stalled socket that stopped sending ticks
- a fault on one source does not cancel the others (`Task.WhenAll` + per-source try/catch)

Simulators can close the socket and replay payloads via configuration, so these paths are easy to exercise without manual socket debugging.

### Monitoring

Thread-safe counters track received, malformed, duplicate, buffered, persisted, and dropped ticks. Every 30 seconds the worker logs that summary plus the current channel depth.

### Shutdown

On host stop, ingestion cancels. The persistence worker then drains the bounded channel and flushes the pending batch, within `Persistence:DrainTimeoutMilliseconds` (default 5 s). If the timeout expires, remaining ticks may be lost — see [Known limits](#known-limits).

## Configuration

Key settings live in `src/ExchangeTickAggregator/appsettings.json` and can be overridden with environment variables (as in Compose).

| Section | Setting | Default | Meaning |
|---|---|---|---|
| `Ingestion` | `BufferCapacity` | 10000 | Bounded channel capacity |
| `Ingestion` | `DeduplicationWindowSeconds` | 10 | Window in which a tick counts as a duplicate |
| `Ingestion` | `IdleTimeoutSeconds` | 15 | How long to wait without ticks before treating the connection as stalled |
| `Ingestion` | `ReconnectInitialDelaySeconds` | 1 | Initial reconnect delay |
| `Ingestion` | `ReconnectMaxDelaySeconds` | 30 | Maximum reconnect delay |
| `Persistence` | `BatchSize` | 500 | Rows per batch |
| `Persistence` | `FlushIntervalMilliseconds` | 1000 | Timer-based flush |
| `Persistence` | `MaxWriteAttempts` | 3 | Write retry budget |
| `Persistence` | `WriteRetryDelayMilliseconds` | 100 | Pause between write retries |
| `Persistence` | `DrainTimeoutMilliseconds` | 5000 | Shutdown drain budget for the channel and pending batch |

Simulator settings: `Simulator__TickIntervalMilliseconds`, `Simulator__DuplicateEveryTicks`, `Simulator__DisconnectAfterTicks`.

## Known limits

- No TLS on simulator WebSockets — local demo only.
- Metrics are log counters, not Prometheus/Grafana.
- Deduplication is in-process only; restarts can accept previously seen ticks again.
- After DB retries are exhausted, the failed batch is dropped (counted and logged), not written to an outbox.
- If shutdown drain exceeds `DrainTimeoutMilliseconds`, ticks still in the channel or pending batch may be lost.
- Simulators listen on `0.0.0.0` so containers can reach each other. Do not expose those ports beyond a trusted host/network.

## Layout

```
src/ExchangeTickAggregator.Core   # parsers, dedup, channel, batch/retry, metrics
src/ExchangeTickAggregator        # worker host, WS ingestion, Postgres sink
src/simulators/*                  # three format-specific WebSocket feeds
tests/ExchangeTickAggregator.Tests
docker-compose.yml
Dockerfile.aggregator
Dockerfile.simulator
```
