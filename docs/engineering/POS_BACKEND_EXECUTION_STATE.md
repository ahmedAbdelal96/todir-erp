# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS
- Branch: `dev`
- Baseline: `46bfc5a06341536a6c5c04523c842cb0fa589188`
- Last meaningful change: secured POS routes with explicit permissions, completed provider/card payment initiation and webhook/recovery convergence, preserved provider idempotency across retries, added sale-line UOM/source references, durable cash-drawer sale movements, bounded return processing with inventory restock recovery, validated offline envelope identity/sequence invariants, added session close blockers plus variance approval, and added idempotent cash-return refund lifecycle through Payments.
- Next executable action: complete provider refund/credit-note ownership, manager overrides, offline package signing/conflict resolution, operational reports, concurrency/security integration tests, and final backend release gates.
- Evidence still valid: disposable PostgreSQL runtime proved authenticated POS register access, a card checkout moving from provider action required to Completed through signed provider success and recovery, a cash checkout with a persisted Sale drawer movement and expected-cash update, bounded damaged and restock return flows, over-return rejection, and migration application for the POS sale-line UOM forward fix. Full M1 certification has not been granted.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PARTIAL | `pos-schema.sql`, forced tenant RLS, runtime role proof, fine-grained route permissions, cash expected-balance locking, and API smoke; full invariant coverage remains |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PARTIAL | Stocked cash/card checkout, durable provider-success recovery, stable payment idempotency, UOM validation, source references, and split tender persistence are proven; failure-injection breadth remains |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL | Cash expected-balance movements, close blockers/variance approval, quantity-safe return/restock recovery, and cash refund lifecycle are implemented; provider refunds/exchanges, lot/serial exactness, credit sale, and override authorization remain |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL | Offline envelopes now validate register/device/payload/sequence and persist conflict records; signed package verification, conflict resolution workflow, fiscal, and hardware contracts remain |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS and bounded sales query exist; concurrency, performance, report read model, authorization matrix, and operational evidence remain |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | NOT COMPLETE | Only basic API/runtime smoke and 2 pure unit tests pass; required contract, integration, security, recovery, and release gates remain |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.
