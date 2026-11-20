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
- Corrected the execution boundary: Manufacturing validates output quantity/product and executable order state before calling Inventory, recovers an Inventory operation by source key after an unknown outcome, and requires a confirmed owner snapshot before committing the Manufacturing fact. Material issue unit cost and the corresponding Material actual-cost fact are derived from Inventory valuation evidence rather than the request body.
- Added request-payload hashes for new material/output events, strict key-reuse conflict detection, co-/by-product output identity validation against the released formula snapshot, and an Inventory operation snapshot capability scoped by tenant and legal entity.
- Removed silent negative-WIP clamping; close readiness reports a negative WIP reconciliation and close is fail-closed.
- Added a forward-only cost-correction migration so signed Inventory returns can reduce WIP without erasing the economic fact, plus Inventory availability reads for MRP netting.
- Added owner-owned Sales and Purchasing development fixtures inside the planning horizon and automatic same-bucket forecast consumption. The planning rule now preserves the unconsumed forecast in gross requirement: 1,000 forecast + 600 accepted Sales consumes 600 forecast and leaves gross/net requirement 1,000 before supply. Finished-goods inventory is netted only after that gross requirement and is scoped by product, plant, UOM, and warehouse pool.
- Corrected Sales source integration to resolve the entered UOM through the Catalog capability and convert to the effective base UOM; corrected development Sales/Purchasing fixtures so their unit IDs/codes match the product variants. The authenticated isolated runtime proved no-stock production 1,000 PACK, 600-stock production 400 PACK, 1,200 accepted-demand production 1,200 PACK, and the independent 100-unit component Purchase supply.
- Re-ran the local gates after the latest source change: Manufacturing 6/6, Inventory 12/12, Sales 2/2, Purchasing 10/10, Projects cross-module 9/9, Security 7/7, Architecture 21/21, full Backend build zero warnings/errors, and Development Seed 5/5 with two seed passes on the isolated database.

## Remaining executable work

- Complete the remaining cross-module demand/capacity integrations: CRM demand ingestion, late-PO exceptions, multi-level explosion, lead-time propagation, transfers, MPS, maintenance downtime, HR labor/resource facts, Ledger semantic facts, Fixed Assets/CIP handoff, and project-cost integration.
- Complete linked lot/serial genealogy acceptance, shop-floor queue/recovery, output unknown-outcome reconciliation, co-/by-product allocation, and full close/cost reconciliation.
- Add isolated acceptance tests for those workflows, including non-empty source-line pegging, concurrent/retry cases, quality fail-closed configuration, and authenticated permission attack cases.
- Resolve the pre-existing development database migration-history mismatch (`expenses-schema.sql` checksum differs from the current repository file) through the repository’s normal forward-fix process before claiming the existing `todir` database migration is complete. The isolated database migration is clean and the existing development volume was preserved.

## Last meaningful change

The final source change corrected gross-requirement arithmetic, warehouse/UOM-scoped stock allocation, Catalog-owned Sales UOM conversion, and the corresponding development fixtures. The isolated migration applied 234 ordered scripts; the authenticated MRP A/B/C runtime cases and affected local gates were recaptured after the final source change.

## Next executable action

Implement MF27–MF35 and MF45–MF47 gaps that are still marked PARTIAL/NOT COMPLETE, beginning with multi-level pegging, downtime/quality/resource seams, and real genealogy/output reconciliation.
