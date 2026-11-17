# Module 25 Banking Backend Execution State

## Scope

Backend, PostgreSQL ownership, migrations, Payments normalization bridge, development seed, API contracts, worker composition, security boundaries, focused tests, and frontend handoff for Module 25.

## Verified state (current checkpoint)

- Canonical `finance_banking` schema and ordered migration are present.
- Treasury accounts, profiles, currency pockets, statement imports/lines, reconciliation sessions/matches, cash counts, cash position, and forecast seed records are persisted.
- Payments account creation consumes `IBankingTreasuryAccountCapability`; legacy PaymentAccount ids remain stable compatibility references.
- Atlas and Delta seed runs pass twice against the disposable PostgreSQL acceptance database.
- API, DbMigrator, and Banking projects build cleanly with zero warnings/errors; Banking tests pass (6/6), including disposable PostgreSQL workflow acceptance for statement validation/classification, forecast version/item creation, maker-checker account changes, connection lifecycle, and repository reads.
- Runtime smoke proof: `/health` returned 200 and unauthenticated `/api/v1/banking/cash-position` returned 401.
- Fresh/upgrade migration applies 218 ordered scripts and platform RLS finalization. The latest forward fixes add workflow projection tables, RLS, and database immutability guards for validated statements, finalized cash counts, and finalized reconciliations.

## Acceptance evidence

Disposable database: `todir_module25_acceptance` on the dedicated test PostgreSQL container at port 15433. No populated development database was changed.

Seed evidence: Atlas and Delta each report 4 statements, 1 reconciliation, 1 cash count, and 1 forecast. The second seed run is idempotent.

## Remaining disposition

The implementation is not yet a final Module 25 completion claim. Remaining Build Now work includes provider/POS settlement reconciliation, bank fee/interest/direct-debit event contracts, cheque visibility, cash-to-GL/report contracts, full authenticated HTTP legal-entity/tenant acceptance, worker/feed replay evidence, migration/opening reconciliation, and runbooks. Heavyweight 100k/1M-row certification was not run by policy and remains an unverified performance gate, not a product defect.

Next executable action: complete the remaining cross-module contracts and authenticated API acceptance, then run the final backend gate after the last source/migration/seed change.
