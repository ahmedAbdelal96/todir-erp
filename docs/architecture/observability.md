# Observability and request pipeline

P03 establishes the minimum request observability contract before business modules exist.

## Request pipeline

The API host keeps the approved order explicit: correlation/request activity, exception handling, security headers, then endpoint execution. Authentication, tenant resolution, authorization context, rate limiting, and module endpoint registration are added only in their approved phases.

`X-Correlation-Id` is accepted only when it parses as a non-empty GUID; otherwise a UUIDv7 correlation ID is created. The value is returned in the response and added to the current activity and structured logging scope with `service`, `environment`, `trace_id`, `span_id`, and `correlation_id`.

## Trace verification

`backend/Tests/Observability` runs a real ASP.NET TestServer request to a handler that executes `SELECT 1` through Npgsql. The test captures .NET Activity spans and asserts one trace contains the HTTP request, handler, and PostgreSQL database span. The CI job supplies an ephemeral PostgreSQL service only for this test path; it is not the production database runtime architecture.

P04 remains responsible for runtime roles, schema ownership, grants, application DB session context, and migration/runtime separation. P03 does not implement those concerns.
