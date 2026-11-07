# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS
- Branch: `dev`
- Baseline: `c2d77a82d1879206fc58f5fd07ad518d7c579e7d`
- Last meaningful change: completed stocked POS issue orchestration, attached the canonical seeded register to Inventory, added explicit checkout recovery, and made recovery idempotent when an Inventory issue already exists.
- Next executable action: complete the remaining M1 failure-injection coverage and then continue the documented POS milestones (card/split tender, returns, offline sync, permissions, reports, concurrency, and release gates).
- Evidence still valid: disposable PostgreSQL runtime proved a real Catalog/Pricing/Tax → cash Payment → Inventory Issue → finalized POS Invoice → Ledger journal flow. A recovery simulation reset the persisted checkout/sale to pending, retried recovery, converged to Completed, and preserved exactly one payment, one Inventory operation, and one finalized invoice. Full M1 certification has not been granted.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PARTIAL | `pos-schema.sql`, forced tenant RLS, runtime role proof, API smoke; fine-grained permissions and full invariant coverage remain |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PARTIAL | Disposable runtime proved 1 completed stocked cash checkout, 1 payment, 1 Inventory issue, 1 finalized POS invoice, and a journal entry reference; recovery converged without duplicates. Split/card and failure-injection coverage remain |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL | Cash/session foundation and pending return persistence exist; full return/refund/exchange, lot/serial, customer/credit, and override authorization remain |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | NOT COMPLETE | Offline envelope storage is present, but signed/versioned package validation, conflict resolution, and fiscal/hardware contracts are not complete |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS and bounded sales query exist; concurrency, performance, report read model, authorization matrix, and operational evidence remain |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | NOT COMPLETE | Only basic API/runtime smoke and 2 pure unit tests pass; required contract, integration, security, recovery, and release gates remain |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.
