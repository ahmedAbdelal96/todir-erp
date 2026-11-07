# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS
- Branch: `dev`
- Baseline: `80e25d313b33c988f674c78243d4c1de7a1608ef`
- Last meaningful change: made checkout drafts durable before any external payment call, serialized concurrent checkout attempts by tenant/idempotency key, persisted provider references and cash/payment tender references after the provider boundary, added a retry-safe exchange completion endpoint, and corrected the exchange read path to use qualified state columns.
- Next executable action: continue with focused POS failure-injection/concurrency proof and final backend release gates.
- Evidence still valid: disposable PostgreSQL runtime proved authenticated POS register access, a card checkout remaining recoverable in `PaymentPending` after a provider timeout, cash checkout with a persisted Sale drawer movement and expected-cash update, two concurrent cash checkout callers converging on one checkout/sale/tender while the later caller received the completed idempotent result, bounded damaged and restock returns, over-return rejection, return credit notes, signed offline package/conflict resolution, mixed tender capture, customer credit sale, a completed exchange with a persisted exchange group, return, replacement checkout, and invoice, exchange completion replay returning the same completed exchange, invalid-signature rejection and valid envelope idempotent replay, and an offline cash sale that completed inventory/document/accounting with one cash movement and one idempotent replay. Full M1 certification has not been granted.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PARTIAL | `pos-schema.sql`, forced tenant RLS, runtime role proof, fine-grained route permissions, cash expected-balance locking, and API smoke; full invariant coverage remains |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PARTIAL | Stocked cash/card checkout, durable pre-provider checkout drafts, provider-reference persistence, durable provider-success recovery, stable payment idempotency, UOM validation, source references, return credit notes, and mixed-tender capture are implemented; failure-injection breadth remains |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL | Cash expected-balance movements, close blockers/variance approval, quantity-safe return/restock recovery, cash/provider refund lifecycle, lot/serial references, customer credit validation, discount overrides, completed exchange orchestration, and retry-safe exchange completion are implemented; failure-injection breadth remains |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; offline packages are versioned/expiring/signed; envelope replay validates package/register/device/signature/sequence; manager conflict resolution and durable offline cash-sale processing are implemented |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS, tenant/idempotency checkout serialization, bounded sales/return/report queries, and migration forward-fixes exist; broader concurrency, performance evidence, full authorization matrix, and operational evidence remain |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | NOT COMPLETE | Only basic API/runtime smoke and 2 pure unit tests pass; required contract, integration, security, recovery, and release gates remain |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.
