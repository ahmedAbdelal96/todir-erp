# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS
- Branch: `dev`
- Baseline: `e6d728815ef89f5c46979c4604d0cdc6c0c1cf83`
- Last meaningful change: added bounded POS reports, preserved lot/serial references through checkout and returns, added customer credit-sale validation and receivable invoice completion, and expanded POS calculation tests to 5.
- Next executable action: complete concurrency/security integration tests, fiscal/hardware seams, and final backend release gates.
- Evidence still valid: disposable PostgreSQL runtime proved authenticated POS register access, a card checkout moving from provider action required to Completed through signed provider success and recovery, a cash checkout with a persisted Sale drawer movement and expected-cash update, bounded damaged and restock return flows, over-return rejection, and migration application for the POS sale-line UOM forward fix. Full M1 certification has not been granted.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PARTIAL | `pos-schema.sql`, forced tenant RLS, runtime role proof, fine-grained route permissions, cash expected-balance locking, and API smoke; full invariant coverage remains |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PARTIAL | Stocked cash/card checkout, durable provider-success recovery, stable payment idempotency, UOM validation, source references, return credit notes, and mixed-tender capture are implemented; failure-injection breadth remains |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL | Cash expected-balance movements, close blockers/variance approval, quantity-safe return/restock recovery, cash/provider refund lifecycle, lot/serial references, customer credit validation, and discount overrides are implemented; exchanges remain |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL | Offline packages are versioned/expiring/signed and envelope replay validates package/register/device/sequence; conflict resolution is manager-authorized; fiscal and hardware contracts remain |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS, bounded sales/return/report queries, and migration forward-fixes exist; concurrency, performance evidence, full authorization matrix, and operational evidence remain |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | NOT COMPLETE | Only basic API/runtime smoke and 2 pure unit tests pass; required contract, integration, security, recovery, and release gates remain |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.
