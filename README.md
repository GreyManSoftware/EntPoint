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

Replace the placeholder `POSTGRES_PASSWORD` and `MONGO_PASSWORD` values in
`.env`. The local `.env` file is ignored by Git; `.env.example` documents the
required variable names without containing usable credentials.

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
docker compose exec mongo sh -c `
  'mongosh --quiet --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin entpoint --eval "db.alerts.find().limit(10)"'
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
`MONGO_USERNAME`, `MONGO_PASSWORD`, and `MONGO_DATABASE` from the ignored
`.env` file.

### Storage design

PostgreSQL stores normalized non-alert events in an `events` table. A composite
index on `(endpoint_id, timestamp DESC)` supports the endpoint summary queries
required in Part 3.

MongoDB stores complete alert documents and maintains indexes for endpoint and
score filtering, score-only filtering, and recent-alert retrieval.

The two stores do not share a distributed transaction. If ingestion is
interrupted after one store has committed, rerun it with `--reset` to restore a
known state.

## Part 3: API and demo page

The ASP.NET Core API queries PostgreSQL for endpoint summaries and MongoDB for
alerts. It also serves a lightweight demonstration page from the same container.

Start the API and its database dependencies:

```powershell
docker compose up -d --build api
```

Open the demo page:

```text
http://localhost:8080
```

The page provides analyst, admin, and cleared-key controls alongside buttons for
endpoint summaries, recent alerts, and filtered alerts. Responses and HTTP
status codes are displayed as formatted JSON.

### API endpoints

Create reusable PowerShell headers with either demo key:

```powershell
$analystHeaders = @{ "X-API-Key" = "entpoint-demo-analyst-key" }
$adminHeaders = @{ "X-API-Key" = "entpoint-demo-admin-key" }
```

List known endpoints:

```powershell
Invoke-RestMethod `
  -Headers $analystHeaders `
  http://localhost:8080/api/v1/endpoints
```

Get an endpoint summary:

```powershell
Invoke-RestMethod `
  -Headers $analystHeaders `
  http://localhost:8080/api/v1/summary/<endpoint-uuid>
```

Get the ten most recent alerts:

```powershell
Invoke-RestMethod `
  -Headers $adminHeaders `
  http://localhost:8080/api/v1/alerts
```

Filter alerts:

```powershell
Invoke-RestMethod `
  -Headers $adminHeaders `
  "http://localhost:8080/api/v1/alerts?endpoint_id=<endpoint-uuid>&min_score=70"
```

`endpoint_id` and `min_score` are optional and can be used independently or
together. Filtered results are returned newest first. Without filters, the API
returns only the ten most recent alerts.

### API errors

- Missing or invalid API key: `401 Unauthorized`
- Authenticated role without permission: `403 Forbidden`
- Invalid endpoint UUID: `400 Bad Request`
- `min_score` outside 1-100: `400 Bad Request`
- Valid endpoint with no relational events: `404 Not Found`
- Alert query with no matches: `200 OK` with an empty array
- Unexpected server or database error: `500 Internal Server Error`

Stop the API without deleting database data:

```powershell
docker compose stop api
```

## Part 4: API security

Every controller endpoint requires an `X-API-Key` header. The exercise uses two
fixed public demonstration keys:

```text
Analyst: entpoint-demo-analyst-key
Admin:   entpoint-demo-admin-key
```

These keys are intentionally hardcoded to satisfy the assessment and require no
generation step. They are not production credentials.

| Endpoint | Analyst | Admin |
|---|---:|---:|
| `GET /api/v1/endpoints` | Allowed | Allowed |
| `GET /api/v1/summary/{endpoint_id}` | Allowed | Allowed |
| `GET /api/v1/alerts` | Forbidden | Allowed |

Request without a key:

```powershell
Invoke-WebRequest `
  http://localhost:8080/api/v1/endpoints `
  -SkipHttpErrorCheck
```

Request with an invalid key:

```powershell
Invoke-WebRequest `
  -Headers @{ "X-API-Key" = "invalid" } `
  http://localhost:8080/api/v1/endpoints `
  -SkipHttpErrorCheck
```

Demonstrate analyst access to summaries:

```powershell
Invoke-WebRequest `
  -Headers $analystHeaders `
  http://localhost:8080/api/v1/summary/<endpoint-uuid>
```

Demonstrate the analyst alert restriction:

```powershell
Invoke-WebRequest `
  -Headers $analystHeaders `
  http://localhost:8080/api/v1/alerts `
  -SkipHttpErrorCheck
```

Demonstrate admin alert access:

```powershell
Invoke-WebRequest `
  -Headers $adminHeaders `
  http://localhost:8080/api/v1/alerts
```

## Design notes

NDJSON supports continuous append-only collection and can be ingested one event
at a time in Part 2. The simulated process table keeps PID/PPID relationships
coherent and ensures file reads belong to known processes. Runs with two or more
machines alternate Windows and Linux assignments so both platforms are
represented; a single machine is assigned one platform when it is created.

API-key authentication uses fixed-time key comparison and a global authenticated
fallback policy. Explicit authorization policies allow analysts and admins to
query endpoint summaries while restricting alerts to admins.
