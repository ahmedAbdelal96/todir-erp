# Observability and request pipeline

P03 establishes the minimum request observability contract before business modules exist.

## Active local topology

The application instrumentation is collected by an opt-in local Prometheus/Alertmanager/Jaeger stack under `ops/monitoring` and `ops/compose/platform-monitoring.yml`. API and Worker metrics are scraped through authenticated internal endpoints; traces use the existing W3C/OpenTelemetry pipeline and an optional OTLP exporter configured by `TODIR_OTEL_EXPORTER_OTLP_ENDPOINT`. The stack is separate from the default ERP development compose file.

## Request pipeline

The API host keeps the approved order explicit: correlation/request activity, exception handling, security headers, then endpoint execution. Authentication, tenant resolution, authorization context, rate limiting, and module endpoint registration are added only in their approved phases.

`X-Correlation-Id` is accepted only when it parses as a non-empty GUID; otherwise a UUIDv7 correlation ID is created. The value is returned in the response and added to the current activity and structured logging scope with `service`, `environment`, `trace_id`, `span_id`, and `correlation_id`.

## Trace verification

`backend/Tests/Observability` runs a real ASP.NET TestServer request to a handler that executes `SELECT 1` through Npgsql. The test captures .NET Activity spans and asserts one trace contains the HTTP request, handler, and PostgreSQL database span. Runtime acceptance additionally starts the configured OTLP receiver and verifies a trace arrives through the exporter path; an in-process listener alone is not sufficient proof.

P04 remains responsible for runtime roles, schema ownership, grants, application DB session context, and migration/runtime separation. P03 does not implement those concerns.

## Daily application files

API and Worker use the shared Serilog configuration from `Shared.BuildingBlocks.Observability`. File output is structured JSON Lines and is written under:

```text
backend/logs/{service}/{yyyy-MM-dd}/application-*.jsonl
backend/logs/{service}/{yyyy-MM-dd}/errors-*.jsonl
```

Interactive console output uses the same structured event data with level colors for operational readability: information is cyan, warnings are yellow, errors are red, and fatal events are bold red. ANSI color codes are omitted when output is redirected, so redirected console output remains valid JSON Lines.

The default retention is 30 days, the default per-file limit is 50 MiB, and size rollover creates additional files without interrupting requests. The directory, retention, size limit, file count limit, minimum level, and file enablement are configurable with `TODIR_LOG_DIRECTORY`, `TODIR_LOG_RETENTION_DAYS`, `TODIR_LOG_FILE_SIZE_BYTES`, `TODIR_LOG_FILE_COUNT_LIMIT`, `TODIR_LOG_MINIMUM_LEVEL`, and `TODIR_FILE_LOGGING_ENABLED`.

Each request completion contains the correlation, trace, span, service, environment, module, operation, status, and duration context. Authenticated requests additionally push tenant, legal-entity, user, and membership context. Request bodies, authorization headers, cookies, passwords, tokens, and sensitive business payloads are never logged by the pipeline. Route logs use endpoint templates rather than raw identifier-bearing request paths. Application logs are operational diagnostics; immutable business audit history remains owned by the relevant module and is never reconstructed from log files.
