# UX, API and performance contract

## UX contract

The future page is a business route under the owning module, recommended shape `/subledger/analysis/collections-hygiene`; the exact registered destination must be reconciled with the current navigation registry during P2. Arabic-first RTL title: **تحليل تحصيلات العملاء**. Do not add a global Analysis sidebar item.

Journey: enter authorized route → choose definition/scope/date semantics/filters → review scope and warnings → press **بدء التحليل** once → observe queued/running status → open saved result → inspect evidence and bounded detail → review recommendation → optionally navigate to the owning workflow. Refresh, filter changes, route entry and browser back never create a second run. Duplicate submissions use idempotency and show the existing run.

List/history views use server pagination and stable cursors. Result sections show source freshness, cutoff, definition version, completeness, currency/UOM/date semantics and warnings. No technical identifiers, SQL errors or stack traces appear in Arabic UI. List and edit/create surfaces remain separate; recommendation review is a focused action surface, not an inline mutation in the result table.

Required future browser/component scenarios include: no auto-run; exactly one explicit run; duplicate-click idempotency; immutable old result after a new run; run-only user sees only own permitted history; unauthorized result/evidence denied; tenant switch clears stale data; mobile RTL overflow; loading/partial/failure/cancelled states; Arabic glossary and permission-aware route discovery.

## Proposed API contract (not implemented)

All routes use the existing `api/v1` convention and must be validated against current endpoint/error/authorization conventions before implementation.

| Method | Proposed route | Purpose | Required permission / side effect |
|---|---|---|---|
| GET | `/api/v1/analysis/definitions` | Catalog visible definitions and versions. | `analysis.catalog.read`; read only. |
| POST | `/api/v1/analysis/runs` | Validate filter snapshot and create one durable run/job. | `analysis.run.create`; idempotency key; no calculation in request. |
| GET | `/api/v1/analysis/runs/{runId}` | Own/history status and provenance summary. | own-read or history-read; read only. |
| POST | `/api/v1/analysis/runs/{runId}/cancel` | Request cooperative cancellation. | `analysis.run.cancel`; audit side effect. |
| GET | `/api/v1/analysis/runs/{runId}/result` | Published immutable result sections. | `analysis.result.read`; cursor for detail. |
| GET | `/api/v1/analysis/runs/{runId}/evidence` | Authorized evidence references/details. | `analysis.evidence.read`; scope recheck. |
| GET | `/api/v1/analysis/runs/{runId}/recommendations` | Recommendations and review state. | recommendation-read; read only. |
| POST | `/api/v1/analysis/recommendations/{id}/review` | Record review decision/note. | `analysis.recommendation.review`; optimistic version; no owner mutation. |
| POST | `/api/v1/analysis/recommendations/{id}/action-request` | Future separately authorized handoff. | Deferred; must revalidate owner workflow. |

DTOs include tenant/scope-safe identifiers, definition/version, filter hash, cutoff, status, timestamps, warnings, completeness, source provenance, cursor and `rowVersion`. Errors use existing problem-code conventions for invalid scope, forbidden, entitlement unavailable, stale version, duplicate idempotency, unsupported temporal semantics, source incomplete, cancelled, timeout and transient failure.

## Performance contract

P0’s local diagnostic/load archive is the baseline context, not a production SLO. P2 must measure API acknowledgement, source reads, computation, publication, result reads, errors, throughput, pool usage, locks, CPU, memory, I/O, worker concurrency, queue age, tenant fairness, cancellation and recovery. Use at least 30 warm repeated samples per scenario, controlled concurrency levels 1/4/16 (or the calibrated safe ceiling), and a separate repeat run; publish p50/p95/p99 only when the sample and error count make the percentile meaningful.

Initial proposed safety limits, subject to calibration and owner approval, are bounded source pages ≤500 facts, one active pilot job per tenant, separate Analysis pool, global worker concurrency 1, job timeout 15 minutes, and no measurable regression beyond the pre-run confidence interval for existing ERP smoke paths. These are proposed guardrails, not accepted production capacity limits. Development Seed cannot certify large-tenant capacity.
