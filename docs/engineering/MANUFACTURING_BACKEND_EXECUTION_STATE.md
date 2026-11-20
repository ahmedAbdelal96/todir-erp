# Module 26 Manufacturing / MRP Backend Execution State

## Delivery status

`NOT COMPLETE`

## Canonical scope

The assigned scope is the backend/database/integration/development-seed delivery defined by `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`, including MF00–MF47. Frontend screens and product/UX acceptance are separate missions.

## Completed and verified

- Added the `Modules.Manufacturing` module, manifest, ownership boundary, shared cost-reader contract, API registration, and Worker registration.
- Added `business_manufacturing` schema, forced tenant RLS, tenant grants, production-definition snapshots, material event ledger, output/scrap facts, cost facts, variance, planning, pegging, genealogy storage, idempotency, and audit storage.
- Implemented plant/profile, BOM/version lifecycle, formula/version, routing/version/operation, substitution, work-center/calendar, definition resolution, production order lifecycle, material conservation, output, scrap, cost fact, and basic MRP APIs.
- Connected Inventory through the existing shared capabilities for reservation, reservation consumption, return receipt, and finished-output receipt. Manufacturing does not write Inventory tables.
- Added a real Worker consumer with Inbox plus Manufacturing idempotency handling for Manufacturing outbox events.
- Added deterministic two-tenant development seed fixtures, including the partial-output and MRP-netting scenarios.
- Verified isolated migration, seed, build, architecture tests, domain tests, security tests, RLS, API health/OpenAPI, and a real local RabbitMQ path. The final runtime probe completed with one published outbox message, one Worker idempotency row, and one integration audit row.
- Added owner-published Sales demand and Purchasing supply capabilities; authenticated MRP execution now refreshes those source snapshots before netting and preserves source ownership.
- Added operation execution events with optimistic concurrency and replay-safe idempotency, capacity read-model calculation, genealogy command/read routes, and close-readiness reporting.
- Revalidated authenticated API paths after the final fixes: production-order and planned-supply reads, MRP run, operation completion plus stale-version replay, close-readiness blocking, and capacity readiness.

## Remaining executable work

- Complete the remaining cross-module demand/capacity integrations: CRM demand ingestion, non-empty Sales/Purchasing source fixtures, late-PO exceptions, multi-level explosion, lead-time propagation, transfers, MPS, maintenance downtime, HR labor/resource facts, Ledger semantic facts, Fixed Assets/CIP handoff, and project-cost integration.
- Complete linked lot/serial genealogy acceptance, shop-floor queue/recovery, output unknown-outcome reconciliation, co-/by-product allocation, and full close/cost reconciliation.
- Add isolated acceptance tests for those workflows, including non-empty source-line pegging, concurrent/retry cases, quality fail-closed configuration, and authenticated permission attack cases.
- Resolve the pre-existing development database migration-history mismatch (`expenses-schema.sql` checksum differs from the current repository file) through the repository’s normal forward-fix process before claiming the existing `todir` database migration is complete. The isolated database migration is clean and the existing development volume was preserved.

## Last meaningful change

The final source change fixed positional PostgreSQL parameter binding in Manufacturing, corrected the Sales owner capability to use the current Sales schema, and made operation idempotency replay succeed before stale-version rejection. The final local API probe then passed authenticated MRP execution and operation replay; the isolated migration/seed, architecture/security tests, formatter, and real RabbitMQ/Worker probe were rerun or freshly verified.

## Next executable action

Implement MF27–MF35 and MF45–MF47 gaps that are still marked PARTIAL/NOT COMPLETE, beginning with non-empty source-line fixtures, multi-level pegging, downtime/quality/resource seams, and real genealogy/output reconciliation; then rerun all affected gates before any completion claim.
