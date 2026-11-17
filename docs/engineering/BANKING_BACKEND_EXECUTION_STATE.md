# Module 25 Banking Backend Execution State

## Scope

Backend, PostgreSQL ownership, migrations, Payments normalization bridge, development seed, API contracts, worker composition, security boundaries, focused tests, and frontend handoff for Module 25.

## Verified state

- Canonical `finance_banking` schema and ordered migration are present.
- Treasury accounts, profiles, currency pockets, statement imports/lines, reconciliation sessions/matches, cash counts, cash position, and forecast seed records are persisted.
- Payments account creation consumes `IBankingTreasuryAccountCapability`; legacy PaymentAccount ids remain stable compatibility references.
- Atlas and Delta seed runs pass twice against the disposable PostgreSQL acceptance database.
- API and Worker build cleanly; Banking tests pass (5/5), including disposable PostgreSQL repository read acceptance.
- Runtime smoke proof: `/health` returned 200 and unauthenticated `/api/v1/banking/cash-position` returned 401.
- Fresh migration applies 216 ordered scripts and platform RLS finalization.

## Acceptance evidence

Disposable database: `todir_module25_acceptance` on the dedicated test PostgreSQL container at port 15433. No populated development database was changed.

Seed evidence: Atlas and Delta each report 4 statements, 1 reconciliation, 1 cash count, and 1 forecast. The second seed run is idempotent.

## Remaining disposition

No mandatory implementation item is left open for frontend integration. Heavyweight 100k/1M-row certification was not run by policy and is recorded as an unverified performance gate, not as a product defect.
