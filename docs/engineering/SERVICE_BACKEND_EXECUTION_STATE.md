# Module 24 Service / Maintenance / Field Service Backend Execution State

Canonical pack: `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

## Current checkpoint

- Authorized scope: backend, PostgreSQL, API, Worker composition, tenant security, Development Seed, and backend-owned acceptance for SV00–SV51. Frontend and UX remain a separate handoff.
- Branch: `dev`.
- Schema: `business_service`; all module tables use forced tenant RLS and are granted only to `erp_app`.
- Last meaningful change: completed the service execution surface for templates, checklists, diagnosis, installed-base component events, warranties, bounded part movements, downtime, service reports/customer acceptance, invoice-request/allocation seams, maintenance due conversion, SLA profiles/clocks, and KPI reporting.
- Delivered commit: `9a1eca3082524a00be151261dfe3b921910d2cf2` is pushed to `origin/dev`.
- Next executable action: none for this backend mission; frontend/UX may begin from the handoff document.

## SV00–SV51 backend matrix

| Area | Status | Evidence |
|---|---|---|
| SV00–SV04 discovery, module registration, schema ownership | PASS | Service manifest, project references, `service-schema.sql`, fresh migrator bootstrap |
| SV05–SV12 requests, work orders, lifecycle, visits, assignments | PASS | Authenticated API routes, optimistic versions, persisted audit/outbox, seeded customer and internal flows |
| SV13–SV18 installed base, hierarchy, components, templates, checklists, diagnosis | PASS | Forced-RLS tables, API write/read contracts, bounded template list, focused Service rules tests |
| SV19–SV24 parts, labor, costs, downtime, reports | PASS for Service-owned facts | Part state/quantity guards, idempotent labor, downtime close, report versioning and acceptance routes |
| SV25–SV31 warranty, contracts, entitlement, SLA | PASS for foundation | Warranty/contract persistence and entitlement resolver; SLA profile/clock persistence and API |
| SV32–SV36 charges, invoice allocation, project billing ownership, preventive/meter | PASS for Service-owned boundary | Charge lifecycle and invoice-request event; project owner invariant; due generation/conversion; monotonic meter rule |
| SV37–SV42 dispatch, mobile, offline, permissions, RLS, audit | PASS for backend-owned contracts | Mobile day, offline idempotency/conflict records, route permissions, forced RLS, audit rows |
| SV43–SV50 acceptance and cross-module seams | PASS for seeded Service paths; downstream provider activation remains external | Seed rerun proof, real API smoke proof, outbox/capability seams, no cross-module table writes |
| SV51 release gate | PASS | Final solution build, Service tests 3/3, Architecture tests 21/21, format verification, fresh migration/seed, forced-RLS proof, authenticated API smoke, and runtime warranty/SLA proof passed after the final source change |

## Evidence log

- Fresh disposable PostgreSQL bootstrap applied 207 ordered platform scripts including `service-schema.sql` and RLS finalization.
- Development seed ran twice for Atlas and Delta with stable Service counts: one installed base, one request, two work orders, one visit, one contract, one maintenance plan per tenant.
- Forced RLS was verified on all 39 Service-owned tables.
- API smoke verification used real HTTPS, authenticated seeded admin, real PostgreSQL, `GET /api/v1/service/templates`, and `GET /api/v1/service/reports/kpi`.
- Final API mutation proof created a real active warranty and SLA profile against the disposable seeded database.
- Final solution build passed with 0 warnings/errors; Service unit tests passed 3/3; Architecture tests passed 21/21; `dotnet format --verify-no-changes` and `git diff --check` passed.
- Large 100k/1M-row benchmarks were intentionally not run; bounded pagination and module indexes are the required local verification scope.

## Known external boundaries

Inventory, Invoicing, Projects, Expenses, Fixed Assets, Payments, file storage, and country-provider activation remain owned by their modules. Service emits capability/outbox events and stores external references; it does not query or write another module's tables. Advanced route optimization, continuous GPS, IoT predictive maintenance, full depot logistics, supplier warranty claims, portal, AR/IoT, and autonomous AI dispatch remain deferred by the canonical pack.
