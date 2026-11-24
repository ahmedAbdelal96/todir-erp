# Observability Foundation — Execution Checkpoint

## Authorized scope

Complete the local opt-in observability, metrics, tracing, alerting, privacy, and operational documentation foundation without changing business-module behavior or shared ERP service ownership.

## Completed and verified

- Shared API and Worker OpenTelemetry registration now supports optional OTLP export with service and environment resource identity.
- API and Worker expose disabled-by-default, authenticated Prometheus endpoints and bounded health endpoints.
- Worker progress is observable and readiness fails when successful processing becomes stale.
- Cache, request, PostgreSQL, and Worker signals are registered without tenant, user, document, query, secret, or raw URL labels.
- Route-template correlation logging and privacy redaction prevent sensitive path values from entering application logs.
- Prometheus, Alertmanager, Jaeger, local sanitized alert receiver, and isolated start/stop scripts are documented and runnable through an opt-in compose project.
- Real local evidence proved authenticated scraping, PostgreSQL trace export, cache metrics, API outage alert firing, recovery notification, and the intentionally unknown data-protection dependency state.

## Remaining deployment dependencies

The repository does not claim production monitoring certification. Deployment still supplies the approved alert destination, secret/TLS management, retention and access controls, multi-instance discovery, resource policy, and provider-owned backup/WAL/restore-drill telemetry. Grafana and external notification integrations remain outside this foundation.

## Last verification

The final runtime verification was performed after the latest source changes on the local `dev` checkout. Task-owned changes must be committed and pushed only after the final diff review; concurrent Quality/frontend changes remain outside this task.
