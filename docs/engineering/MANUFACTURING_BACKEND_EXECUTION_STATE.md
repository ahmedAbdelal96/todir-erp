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

## Remaining executable work

- Complete the full cross-module demand/capacity integrations: Sales/CRM demand ingestion, Purchasing planned supply and late-PO exceptions, multi-level explosion, lead-time propagation, transfers, MPS, maintenance downtime, quality fail-closed gates, HR labor/resource facts, Ledger semantic facts, Fixed Assets/CIP handoff, and project-cost integration.
- Complete genealogy commands/read APIs, shop-floor commands and retry recovery, operation execution, output reconciliation/unknown-outcome recovery, co-/by-product allocation, and production close/readiness reporting.
- Add isolated integration acceptance tests for the above workflows and authenticated API/RLS permission attack cases.
- Resolve the pre-existing development database migration-history mismatch (`expenses-schema.sql` checksum differs from the current repository file) through the repository’s normal forward-fix process before claiming the existing `todir` database migration is complete. The isolated database migration is clean and the existing development volume was preserved.

## Last meaningful change

Manufacturing close guards were hardened to reject unreconciled output, quality, and cost states; the runtime manifest now includes canonical manufacturing permissions; the output command now reports the committed receipt state. The final build, scoped tests, formatter, isolated migration/seed, API runtime, RLS probe, and RabbitMQ/Worker probe were rerun after this change.

## Next executable action

Implement the remaining MF27–MF35 integration/readiness workflows, add their isolated acceptance tests, rerun the affected migration/seed/runtime gates, then update this checkpoint and the frontend handoff.
