# Module 25 Banking Backend Execution State

## Scope

Backend, PostgreSQL ownership, migrations, Payments normalization bridge, development seed, API contracts, worker composition, security boundaries, focused tests, and frontend handoff for Module 25.

## Verified state (current checkpoint)

- Canonical `finance_banking` schema and ordered migration are present.
- Treasury accounts, profiles, currency pockets, statement imports/lines, reconciliation sessions/matches, cash counts, cash position, and forecast seed records are persisted.
- Payments account creation consumes `IBankingTreasuryAccountCapability`; legacy PaymentAccount ids remain stable compatibility references.
- Atlas and Delta seed runs pass twice against the disposable PostgreSQL acceptance database.
- API, Worker, DbMigrator, and Banking projects build cleanly with zero warnings/errors; Banking PostgreSQL tests pass (11/11) plus four Banking rules tests. Disposable PostgreSQL acceptance covers statement validation/classification, forecast version/item creation, maker-checker account changes, connection lifecycle, repository reads, external-feed replay/conflict protection, POS close projection idempotency, provider settlement gross/fee/adjustment/net preservation, bank-originated approval outbox evidence, idempotent unknown receipt orchestration, internal transfer evidence reconciliation, 1:1/1:M/M:1/M:M and partial reconciliation cardinality, bank activity reporting, opening reconciliation provenance/finalization, and optimistic POS/provider settlement confirmation and matching. Payments cross-currency transfer and unallocated receipt acceptance, Ledger bank-originated posting acceptance, and authenticated Banking HTTP isolation acceptance also pass.
- Runtime smoke proof: `/health` returned 200 and unauthenticated `/api/v1/banking/cash-position` returned 401.
- Fresh/upgrade migration applies 228 ordered scripts and platform RLS finalization. The latest forward fixes add workflow projection tables, provider settlement and POS close projections, reconciliation system-item cardinality storage, bank-originated Ledger semantic accounts/rules, settlement lifecycle versioning and provider-line linkage, internal-transfer currency/version and FX-evidence columns, opening reconciliation provenance and lifecycle, RLS, and database immutability guards for validated statements, finalized cash counts, and finalized reconciliations.

## Acceptance evidence

Disposable database: `todir_module25_acceptance` on the dedicated test PostgreSQL container at port 15433. No populated development database was changed.

Seed evidence: Atlas and Delta each report 4 statements, 1 reconciliation, 1 cash count, and 1 forecast. The second seed run is idempotent.

## Remaining disposition

The implementation is ready for final Module 25 backend handoff. Authenticated HTTP proof covers anonymous denial, restricted Atlas/Delta own-entity access, cross-entity denial, cross-tenant denial, and masked sensitive data. Live RabbitMQ replay dispatched an approved `UnknownReceipt` event through the registered Worker consumer and produced one completed unallocated Payments receipt with no party or Subledger item; Inbox recorded the message once. Internal transfer reconciliation is implemented with explicit source/destination currency snapshots, row-locked statement evidence, one-sided `InTransit`, two-sided `Matched`, and optimistic versioning. Opening reconciliation preserves source-system, legacy-account, batch, and opening-balance provenance and requires a posted/locked Ledger opening batch before finalization. Bank activity, cash-to-Ledger, and Payments-owned cheque visibility contracts are implemented. Operational procedures are documented in `BANKING_BACKEND_RUNBOOK.md`. Heavyweight 100k/1M-row certification was not run by policy and remains an explicitly unverified performance gate, not a product defect.

Next executable action: commit the verified Module 25 backend handoff and push `dev` to `origin`.
