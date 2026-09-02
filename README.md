# EntPoint

EntPoint is a simulated endpoint security-data pipeline implemented in C#.

## Current system flow

```text
Virtual Windows/Linux endpoints
	→ EntPoint.Collector
	→ normalization and filtering
	→ data/events.ndjson
	→ EntPoint.Ingestion
	├→ non-alert events → PostgreSQL events table
	└→ alert events     → MongoDB alerts collection
```

The Collector and Ingestion applications are separate C# executables and
containers. The Collector appends normalized events to NDJSON. The one-shot
Ingestion container reads that file, sends each batch to the appropriate
database over the private Compose network, and then exits.

PostgreSQL and MongoDB are persistent services backed by named Docker volumes.
Compose provides the ingestion container with connection settings and resolves
the service names `postgres` and `mongo` through Docker DNS.

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

Create the local Compose environment file before the first run:

```powershell
Copy-Item .env.example .env
```

Replace the placeholder `POSTGRES_PASSWORD` value in `.env`. The local `.env`
file is ignored by Git; `.env.example` documents the required variable names
without containing a usable credential.

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

## Part 2: storage and ingestion

Non-alert events are stored in PostgreSQL. Alert events are stored in the
MongoDB `alerts` collection. Both databases run locally in Docker with named
volumes, so their data persists when the containers stop.

Start the databases:

```powershell
docker compose up -d postgres mongo
```

Ingest `data/events.ndjson`:

```powershell
docker compose run --rm ingestion
```

Use `--reset` to clear both stores before importing:

```powershell
docker compose run --rm ingestion --reset
```

The ingestion container exits after processing the file. PostgreSQL and MongoDB
continue running until stopped:

```powershell
docker compose stop postgres mongo
```

The ingestion application can also run locally while the database containers
are running. Set all service connection values explicitly before launching:

```powershell
$env:ENTPOINT_POSTGRES = "<PostgreSQL connection string>"
$env:ENTPOINT_MONGO = "<MongoDB connection string>"
$env:ENTPOINT_MONGO_DATABASE = "<MongoDB database name>"

dotnet run --project .\src\EntPoint.Ingestion\EntPoint.Ingestion.csproj -- `
  --input .\data\events.ndjson `
  --reset
```

### Inspect stored data

Count PostgreSQL events:

```powershell
docker compose exec postgres `
  psql -U entpoint -d entpoint -c "SELECT COUNT(*) FROM events;"
```

Inspect MongoDB alerts:

```powershell
docker compose exec mongo `
  mongosh --quiet entpoint --eval "db.alerts.find().limit(10)"
```

### Ingestion options

```text
--input <path>             NDJSON input path
--postgres <connection>    PostgreSQL connection string
--mongo <connection>       MongoDB connection string
--mongo-database <name>    MongoDB database name
--reset                    Clear both stores before ingestion
```

The equivalent environment variables are `ENTPOINT_INPUT_PATH`,
`ENTPOINT_POSTGRES`, `ENTPOINT_MONGO`, and `ENTPOINT_MONGO_DATABASE`.
PostgreSQL, MongoDB, and the MongoDB database name must be supplied through
their environment variables or corresponding command-line arguments. The
application contains no service connection fallbacks.

Docker Compose reads `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`,
`MONGO_CONNECTION_STRING`, and `MONGO_DATABASE` from the ignored `.env` file.

### Storage design

PostgreSQL stores normalized non-alert events in an `events` table. A composite
index on `(endpoint_id, timestamp DESC)` supports the endpoint summary queries
required in Part 3.

MongoDB stores complete alert documents and maintains indexes for endpoint and
score filtering, score-only filtering, and recent-alert retrieval.

The two stores do not share a distributed transaction. If ingestion is
interrupted after one store has committed, rerun it with `--reset` to restore a
known state.

## Design notes

NDJSON supports continuous append-only collection and can be ingested one event
at a time in Part 2. The simulated process table keeps PID/PPID relationships
coherent and ensures file reads belong to known processes. Runs with two or more
machines alternate Windows and Linux assignments so both platforms are
represented; a single machine is assigned one platform when it is created.

Part 3 will add an ASP.NET Core API over the PostgreSQL and MongoDB stores.
Part 4 will add API-key authentication and role-based authorization.
