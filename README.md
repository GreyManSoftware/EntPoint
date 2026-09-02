# EntPoint

EntPoint is a simulated endpoint security-data pipeline implemented in C#.

## Part 1: event collection simulation

The collector creates one or more virtual Windows and Linux machines, each with
its own UUID and initial mock process inventory. It then continuously generates
OS-specific process-start and file-read events. Each event records its endpoint,
operating system, user, process name, PID, PPID, and an ISO 8601 UTC timestamp.
File-read events also include a platform-appropriate file path.

A configurable percentage of events include an alert score and reason.
Normalization derives `is_alert`, rejects invalid data, and filters
`system_idle_process` and `svchost.exe`. Accepted events are appended as NDJSON.

## Run with Docker

Build and start continuous collection:

```powershell
docker compose up --build collector
```

Stop the collector with `Ctrl+C`. Events are written to `data/events.ndjson`.

Run a finite, repeatable sample:

```powershell
docker compose run --rm collector `
  --output /app/data/events.ndjson `
  --machines 2 `
  --max-events 25 `
  --interval-ms 10 `
  --seed 42
```

Run the tests:

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

## Design notes

NDJSON supports continuous append-only collection and can be ingested one event
at a time in Part 2. The simulated process table keeps PID/PPID relationships
coherent and ensures file reads belong to known processes. Runs with two or more
machines alternate Windows and Linux assignments so both platforms are
represented; a single machine is assigned one platform when it is created.

Later parts will add PostgreSQL for ordinary events, MongoDB for alerts, an
ASP.NET Core query API, and API-key authorization. All services will run locally
through Docker Compose.
