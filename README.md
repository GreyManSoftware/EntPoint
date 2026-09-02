# EntPoint

EntPoint is a simulated endpoint security-data pipeline implemented in C#.

## Collector

`EntPoint.Collector` simulates security telemetry from one or more virtual
Windows and Linux endpoints. Each endpoint receives a UUID and an initial
process inventory before continuously generating process-start and file-read
events with coherent PID/PPID relationships.

Events include the endpoint operating system, user, process, UTC timestamp, and
platform-appropriate file information. A configurable percentage also include
an alert score and reason. The normalizer validates the events, derives
`is_alert`, filters denylisted processes, and appends accepted events as NDJSON.

## Run locally

.NET 10 SDK is required.

Start continuous collection from the repository root:

```powershell
dotnet run --project .\src\EntPoint.Collector\EntPoint.Collector.csproj
```

Run a finite, repeatable sample:

```powershell
dotnet run --project .\src\EntPoint.Collector\EntPoint.Collector.csproj -- `
  --output .\data\events.ndjson `
  --machines 2 `
  --max-events 25 `
  --interval-ms 10 `
  --seed 42
```

Press `Ctrl+C` to stop continuous collection.

## Run in Docker

Build and start continuous collection:

```powershell
docker compose up --build collector
```

Events are written to `data/events.ndjson`. Press `Ctrl+C` to stop collection.

Run a finite, repeatable sample:

```powershell
docker compose run --rm collector `
  --output /app/data/events.ndjson `
  --machines 2 `
  --max-events 25 `
  --interval-ms 10 `
  --seed 42
```

The VS Code container debugging profile writes persistent output to
`debug_data/events.ndjson`, keeping debug data separate from normal runs.

## Run tests

Locally:

```powershell
dotnet test .\EntPoint.slnx
```

In Docker:

```powershell
docker compose run --rm tests
```

## Collector options

```text
--output <path>              NDJSON output path
--interval-ms <number>       Delay between continuous events
--max-events <number>        Stop after writing this many events
--machines <number>          Number of virtual machines (default: 1)
--initial-processes <number> Initial process inventory size
--alert-percentage <number>  Alert frequency from 0 to 100
--seed <number>              Fixed random seed
```

If `--max-events` is omitted, collection continues until cancelled. Output files
are opened in append mode.

## Design notes

NDJSON supports continuous append-only collection and can be ingested one event
at a time in Part 2. The simulated process table keeps PID/PPID relationships
coherent and ensures file reads belong to known processes. Runs with two or more
machines alternate Windows and Linux assignments so both platforms are
represented; a single machine is assigned one platform when it is created.

Later parts will add PostgreSQL for ordinary events, MongoDB for alerts, an
ASP.NET Core query API, and API-key authorization. All services will run locally
through Docker Compose.
