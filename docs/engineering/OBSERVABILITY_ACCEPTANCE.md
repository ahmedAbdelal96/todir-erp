# Observability and Monitoring Foundation — Local Acceptance

Status: COMPLETE for the local development foundation; production certification is deferred.

## Scope

This acceptance covers the provider-neutral backend observability foundation: bounded OpenTelemetry traces and metrics, authenticated internal Prometheus endpoints, an opt-in local Prometheus/Alertmanager/Jaeger stack, worker progress health, privacy-safe logs, and an operator runbook. It does not add business-module instrumentation, a Grafana deployment, or a production notification/backup provider.

## Coverage matrix

| Requirement | Implementation | Local proof | Status |
| --- | --- | --- | --- |
| Request, handler, and PostgreSQL traces | Shared OpenTelemetry registration with optional OTLP export | Jaeger received trace `4699b8cc2e44ef1a95809891a64b175f` for `GET /api/v1/finbase/currencies`; one trace contained HTTP, handler, and `postgresql` spans | PASS |
| Bounded API metrics | Core request/latency counters and histograms use method, route template, and status class | Authenticated scrape from API returned `todir_http_request_duration_seconds` and `target_info` with service `Api` and Development environment | PASS |
| Cache and database signals | Registered cache meter plus ASP.NET Core and Npgsql instrumentation | Real authenticated currency request returned `200`; API scrape exposed `todir_cache_hits_total` for `finbase.currency.reference` and Npgsql connection metrics | PASS |
| Worker progress and health | Worker meter, cycle age/success age gauges, failure and processed counters, live/ready endpoints | Worker `/health/live`, `/health/ready`, and authenticated `/internal/metrics` returned `200`; unauthenticated metrics returned `404` | PASS |
| Scrape and alert delivery | Opt-in Prometheus rules and Alertmanager local receiver | API stop produced `ApiUnavailable` firing; restart produced the corresponding `resolved` record in sanitized `artifacts/monitoring/alerts.jsonl` | PASS |
| Privacy and access control | Bearer token or loopback-only metrics access; endpoint-template logs; no raw request-path/query enrichment | API and Worker unauthenticated metrics returned `404`; authenticated metrics returned `200`; observability tests passed `10/10`, including sensitive-path redaction | PASS |
| Data-protection truthfulness | Deployment-owned backup/WAL adapter metrics are explicit and unknown when absent | Prometheus showed `data-protection` target down and emitted warning/critical unknown alerts; no fabricated backup health was reported | PASS with deployment dependency |
| Local operational isolation | Separate compose project, loopback ports, named monitoring volumes, bounded receiver evidence | Prometheus `19090`, Alertmanager `19093`, Jaeger `16686`, and OTLP `14317/14318` ran without changing ERP service volumes | PASS |
| Monitoring overhead | Representative bounded health probe against the metrics-enabled API | 100 local liveness requests: all `200`, 438.59 ms elapsed, 203.12 ms process CPU, 0.31 MiB working-set delta; this is not a capacity claim | PASS |

## Test evidence

- Observability tests: `10 passed, 0 failed`.
- Worker Release build: `0 warnings, 0 errors`.
- API Release build: `0 warnings, 0 errors`.
- Architecture tests: `23/23 passed`.
- Security tests: `7/7 passed` against the isolated local acceptance database.
- Redis platform integration: `12/12 passed` against PostgreSQL plus Docker Redis, including stale-fill, coalescing, cancellation, privacy, tenant/permission, and outage/recovery contracts.
- Prometheus configuration and rules: `promtool check config` passed; `12 rules` loaded successfully.
- Runtime API login and currency read: `200`; controlled Development-only observability failure endpoint: authenticated `500` and loopback restricted.

## Operator handoff

Use [observability.md](../runbooks/observability.md) for start/stop, scrape, alert, trace, privacy, and investigation procedures. The local stack is intentionally opt-in. Before deployment, provide an approved notification receiver, secret/TLS distribution, trace/log retention, service discovery, resource limits, and a real data-protection telemetry adapter for backup, WAL, and restore-drill signals.

The absence of the deployment-owned data-protection adapter is an explicit remaining deployment dependency, not a failed application implementation and not production recovery certification.
