# ADR-019 — OpenTelemetry Observability

Status: Accepted

## Decision

Use OpenTelemetry for request, handler and database traces with W3C correlation. Operational metrics use stable service/operation dimensions and never use tenant identifiers as unbounded metric labels; alerts are defined for API failures, worker health and outbox age.

## Consequences

Cross-service failures can be correlated without exposing tenant data in metric cardinality, and monitoring configuration remains deployable independently of business modules.

## Evidence

`backend/Shared/BuildingBlocks/Observability`, `ops/monitoring/alerts.yml`, and the observability test project.
