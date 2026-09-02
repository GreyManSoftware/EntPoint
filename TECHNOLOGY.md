# Technology Stack and Design Rationale

## C# and .NET 10

C# is the language I am most familiar with, which allowed me to prototype the
solution quickly while retaining strong typing, clear domain models, and
reliable asynchronous code. It is also a mature, well-supported,
enterprise-level engineering language.

Modern .NET runs across Windows and Linux and supports common x64 and ARM64
architectures. This made it suitable for a platform-agnostic exercise and
allowed the applications to run consistently both locally and in Linux
containers.

## ASP.NET Core

ASP.NET Core provides the REST API, dependency injection, middleware pipeline,
JSON handling, authentication, and authorization. Controllers keep the required
endpoints easy to identify and allow role policies to be applied without
coupling security logic to database queries.

## PostgreSQL

PostgreSQL stores the high-volume, non-alert event data. A relational schema is
appropriate because normalized events have a consistent structure and the
summary endpoint relies on counting and grouping records. Constraints protect
the stored shape, while the endpoint and timestamp index supports the primary
query pattern.

## MongoDB

MongoDB stores the lower-volume security alerts. Alerts are naturally
represented as self-contained documents and include optional event-specific
fields. Compound indexes support endpoint and score filtering, while a
timestamp index supports retrieval of the most recent alerts.

## NDJSON

Newline-delimited JSON provides a simple boundary between collection and
ingestion. The Collector can append and flush one event at a time, while the
Ingestion application can process the file line by line without loading one
large JSON document. It also leaves an inspectable artifact between pipeline
stages.

## Docker and Docker Compose

Docker isolates the application and database dependencies from the host.
Docker Compose provides repeatable service configuration, health checks,
network-based service discovery, bind mounts, and persistent database volumes.
This keeps setup local and avoids requiring cloud infrastructure.

Actual local credentials are held in ignored environment files. The committed
`.env.example` documents the required values without publishing usable
credentials.

## Npgsql and MongoDB.Driver

Npgsql and the official MongoDB C# driver communicate with the databases using
their native protocols. This avoids shelling out to `psql` or `mongosh` from the
applications and gives the persistence layer parameterized queries, typed
values, transactions, filters, and index-aware operations.

## API-key Authentication

API-key authentication was chosen because it satisfies the assessment without
introducing the additional infrastructure and lifecycle associated with JWT
issuance. The two hardcoded keys are intentionally public demonstration values
required for testing the analyst and admin roles.

A production implementation would use securely stored, hashed, rotatable keys
with revocation and audit support.

## xUnit

xUnit provides focused coverage for simulation, normalization, option parsing,
ingestion routing, alert mapping, controllers, key validation, and authorization
policies. The same suite runs locally and in Docker.

## Review Interface

The HTML, CSS, and vanilla JavaScript page is a review aid rather than a product
component. It is served by the API container to avoid introducing a separate
frontend framework, build system, or container. Its purpose is to make the API
routes, role behavior, status codes, and JSON responses easy to demonstrate.

## Trade-offs

- The endpoint telemetry is simulated rather than collected from real operating
  systems, keeping the implementation aligned with the assessment.
- Ingestion is a one-shot batch process using the NDJSON file as its input.
- PostgreSQL and MongoDB do not share a distributed transaction. The explicit
  `--reset` option provides a repeatable recovery path for this exercise.
- Filtered alert requests are not paginated because pagination was outside the
  requested scope.
- The review interface and hardcoded API keys are demonstration features and
  would not be shipped as production components.
