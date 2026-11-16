# Module 24 Backend Coverage Matrix

Canonical pack: `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

`PASS` means the requirement is implemented and exercised after the last relevant source/migration change against the disposable PostgreSQL acceptance database.

| Canonical requirement | Database/API/business coverage | Integration and security proof | Status | Evidence |
|---|---|---|---|---|
| Service request and Work Order lifecycle | Requests, triage, Work Orders, visits, assignments, optimistic transitions, audit and outbox | Operational acceptance plus forced RLS and resource legal-entity lookup | PASS | `ServiceOperationalAcceptanceTests` |
| Installed Base, hierarchy and component replacement | Installed Base ownership, parent hierarchy, event history, replacement/removal and cycle guard | Cross-tenant read denial and component replacement assertion | PASS | `ServiceOperationalAcceptanceTests` |
| Templates, checklist, diagnosis and service report | Versioned template snapshot, typed checklist values, required answers, diagnosis, report versions and signed Platform file reference | Accepted reports are immutable; invalid Measurement answers are rejected | PASS | `ServiceOperationalAcceptanceTests` |
| Preventive plans, due items and maintenance rounds | Bounded/idempotent due generation, conversion, postponement, stale-version rejection, round/subject completion | Seeded operational acceptance | PASS | `ServiceOperationalAcceptanceTests` |
| Meter readings and rollover/reset | Typed UOM readings, duplicate idempotency, decrease/reset guard and meter-triggered due work | Seeded operational acceptance | PASS | `ServiceOperationalAcceptanceTests` |
| Inventory reservation, issue, consume, return and reverse | Service ledger is pending/processed/retryable/reversed; Inventory capability owns stock truth and operation reversal | Real Inventory operations and reconciled Service counters | PASS | `ServiceCrossModuleAcceptanceTests` |
| Labor, cost facts, downtime and reliability | Idempotent labor/cost fact, downtime lifecycle, failure count, MTBF, MTTR and availability KPI calculations | Persisted cost/downtime and KPI assertions | PASS | `ServiceOperationalAcceptanceTests` |
| Warranty, contract and line-level entitlement | Customer/supplier warranty rules, provider validation, contract usage caps, effective dates, overage and persisted coverage decisions | Supplier recovery state/idempotency and line-level entitlement assertions | PASS | `ServiceOperationalAcceptanceTests` |
| SLA calendar, pause/resume and breach | Versioned working-week/calendar window, pause extension, breach monitor and worker scope | Real clock state and scheduled-worker cycle | PASS | `ServiceOperationalAcceptanceTests` |
| Charges, Invoicing and Project ownership | Coverage-derived charges, corrective charge links, partial allocation, invoice-line reference, source validation and one billing owner | Invoicing draft/allocation and Projects cost fact acceptance | PASS | `ServiceCrossModuleAcceptanceTests` |
| Offline typed command execution | Typed commands, idempotent client replay, Pending/Processed/Rejected/Retryable/Conflict states and stale-version conflict | Replay and conflict assertions; no synthetic accepted-only result | PASS | `ServiceOperationalAcceptanceTests`, `ServiceCrossModuleAcceptanceTests` |
| Authorization, tenant/legal-entity isolation and audit | Backend permission routes, resource-level entity lookup, forced RLS and append-only audit paths | Architecture 21/21, database guard and cross-tenant denial | PASS | Service database integrity and architecture gates |
| Development Seed | Deterministic Atlas/Delta Service data with real Catalog, Inventory, Fixed Assets, Projects and Invoicing references | Fresh migration plus rerunnable SeedData gate | PASS | `SeedDataTests` 5/5 |
| SV00–SV51 backend release acceptance | Build Now surface, SV22–SV50 workflows, ownership boundaries and release evidence | Fresh PostgreSQL/seeded runtime proof; frontend deliberately excluded | PASS | Service acceptance 4/4, unit 5/5, Architecture 21/21, SeedData 5/5 |

Deferred exactly as documented by the canonical pack: advanced route optimization, continuous GPS, IoT predictive maintenance, advanced spare-parts optimization, full depot repair logistics, full supplier warranty claims, advanced EHS/fleet, autonomous AI dispatch/warranty, complex global contract revenue recognition, customer portal, and remote AR/IoT.
