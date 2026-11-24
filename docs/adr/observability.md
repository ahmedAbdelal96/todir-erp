# ADR-019 — OpenTelemetry Observability

Status: Accepted

## Decision

Use OpenTelemetry for request, handler and database traces with W3C correlation. Operational metrics use stable service/operation dimensions and never use tenant identifiers as unbounded metric labels. API and Worker expose authenticated internal Prometheus endpoints only when explicitly enabled; an optional OTLP exporter sends traces to the isolated local Jaeger receiver or an approved provider. Alertmanager delivers only to an approved deployment receiver; local verification uses a disposable receiver.

## Consequences

Cross-service failures can be correlated without exposing tenant data in metric cardinality, and monitoring configuration remains deployable independently of business modules. Worker readiness is based on successful processing progress, not process existence alone. Missing deployment-owned backup/WAL metrics remain visibly unknown instead of becoming fabricated healthy values.

## Evidence

`backend/Shared/BuildingBlocks/Observability`, `ops/monitoring/alerts.yml`, and the observability test project.
