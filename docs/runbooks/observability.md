# Observability and monitoring runbook

This is the local, provider-neutral monitoring contract. It is an opt-in stack and does not change the shared ERP database, RabbitMQ, Redis, or MinIO volumes.

## Start and stop

From the repository root:

```powershell
pwsh -NoProfile -File ops/monitoring/receive-alerts.ps1
pwsh -NoProfile -File ops/monitoring/start.ps1
```

Start the API with `TODIR_METRICS_ENABLED=true`, `TODIR_METRICS_TOKEN` set to the contents of `artifacts/monitoring/metrics-token`, and an address reachable by the monitoring containers (for example `http://0.0.0.0:5091`). Start the Worker with `TODIR_WORKER_METRICS_ENABLED=true`, the same token, and `TODIR_WORKER_METRICS_URL=http://0.0.0.0:5081`. The Prometheus targets are then `api:5091`, `worker:5081`, and the optional data-protection adapter at `9105` through Docker Desktop's host gateway.

The stack contains Prometheus, Alertmanager, and a small Jaeger OTLP receiver. Grafana is intentionally not included: Prometheus and Jaeger provide the required local query and trace proof without adding an authenticated dashboard service that the development environment does not need.

```powershell
pwsh -NoProfile -File ops/monitoring/stop.ps1
pwsh -NoProfile -File ops/monitoring/stop.ps1 -RemoveData
```

`-RemoveData` removes only the monitoring volumes. It does not stop or delete ERP services.

## Investigation sequence

1. Check `http://127.0.0.1:19090/targets` and confirm API and Worker are `UP`.
2. Check the active alert and its `service`, `job`, `instance`, and `trace_id` annotations.
3. Open the related JSONL application/error log using the correlation ID. Request logs use route templates rather than raw identifier-bearing URLs.
4. Query Jaeger using the service name and time window. The W3C trace context follows HTTP, handler, Npgsql, Outbox, RabbitMQ, Worker, and Inbox boundaries when those boundaries are active.
5. Check PostgreSQL, RabbitMQ, Redis, and Outbox state before restarting anything. Restore the failing dependency, then confirm the alert is resolved and the successful-cycle/health signal has recovered.

## Metric contract

The API exports request count and duration, with bounded `method`, route-template endpoint, and `status_class` dimensions. `4xx` and `5xx` are separate; low traffic is protected by a non-zero denominator in the error-rate alert. The Worker exports scrape availability, cycle age, successful-cycle age, Outbox pending count/oldest age, publish/failure counters, and processed-message counters. The Redis meter is explicitly registered and exports cache hits, misses, failures, invalidations, coalescing, stampede waits, and oversized-entry skips.

No tenant, user, document, search, request-body, SQL-parameter, raw exception, or raw identifier-bearing URL is a metric label. Data-protection alerts are `UNKNOWN` when the deployment-owned provider adapter is absent; sanitized recovery evidence is not silently converted into a fake continuous backup signal.

## Safe alert delivery test

Keep the receiver local. For a repeatable availability drill, stop only the disposable API process that is being scraped, wait for `ApiUnavailable` to fire, inspect the sanitized `artifacts/monitoring/alerts.jsonl`, restart the same API build, and wait for the corresponding `resolved` record. For an application-error drill, enable the Development-only `TODIR_OBSERVABILITY_TEST_FAILURE_ENABLED` flag and protect `/internal/observability/test-failure` with `TODIR_OBSERVABILITY_TEST_TOKEN`; never expose that route outside loopback or a disposable local instance. Never point `alertmanager.yml` at a customer or production destination during local testing. Alertmanager groups by alert name/job/service and sends resolved notifications for recovery verification.

## Dependency semantics

- `/health/live` means the process can answer and does not query dependencies.
- API `/health/ready` requires a bounded PostgreSQL `select 1`; Redis is optional because PostgreSQL fallback is supported.
- Worker `/health/ready` requires a recent successful bounded processing cycle; a running but stalled process is not healthy.
- RabbitMQ and Outbox health are diagnosed from Worker scrape metrics, broker state, and database backlog age.
- Redis failure is a degraded-cache condition when PostgreSQL fallback remains available, not an ERP outage.

## Data protection

Backup success means an artifact was created; verified recovery means the local PITR/restore drill completed. Keep those concepts separate. The deployment-owned backup adapter must publish last verified backup, failure, repository, WAL, and restore-drill metrics; if it is missing, the monitoring alert must say `UNKNOWN` rather than report healthy values.

## Production dependencies

Production still requires an approved notification destination, secret/TLS distribution, trace and log retention, resource limits, multi-instance scrape discovery, provider-specific backup/WAL exporters, and authenticated access to Prometheus/Alertmanager/Jaeger. This local stack is operational evidence, not production monitoring certification.
