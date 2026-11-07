# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: NOT COMPLETE
- Branch: `dev`
- Baseline: `7e25189650449eefdffbd8bc571d771b2bc510b8`
- Last meaningful change: closed PostgreSQL data readers before transaction commit, bound opened sessions to the authenticated actor, and made POS cart lines require Catalog/Pricing context plus authoritative Tax calculation and tax snapshots.
- Next executable action: implement the remaining authoritative tax, inventory, payment, invoicing, ledger, recovery, return, offline-sync, authorization, and acceptance paths before any release commit.
- Evidence still valid: POS project, manifest, schema/RLS baseline, runtime permission forward-fix, API composition, seed, and disposable PostgreSQL smoke verification exist locally. No release certification has been granted.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PARTIAL | `pos-schema.sql`, forced tenant RLS, runtime role proof, API smoke; fine-grained permissions and full invariant coverage remain |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | NOT COMPLETE | Current checkout persists a pending/recovery state and emits a recovery message; downstream economic effects are not completed exactly once |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL | Cash/session foundation and pending return persistence exist; full return/refund/exchange, lot/serial, customer/credit, and override authorization remain |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | NOT COMPLETE | Offline envelope storage is present, but signed/versioned package validation, conflict resolution, and fiscal/hardware contracts are not complete |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS and bounded sales query exist; concurrency, performance, report read model, authorization matrix, and operational evidence remain |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | NOT COMPLETE | Only basic API/runtime smoke and 2 pure unit tests pass; required contract, integration, security, recovery, and release gates remain |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.
