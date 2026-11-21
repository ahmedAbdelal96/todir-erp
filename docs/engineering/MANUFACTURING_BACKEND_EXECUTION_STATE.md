# Module 26 Manufacturing / MRP Backend Execution State

## Delivery status

`BACKEND COMPLETE + DEVELOPMENT SEED READY — FRONTEND HANDOFF`

## Canonical scope

The delivered scope is the backend, database, integration, Worker, security, migration and Development Seed capability defined by `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`, MF00–MF47. Frontend implementation and Product/UX acceptance remain separate missions.

## Completed and verified

- Manufacturing owns its `business_manufacturing` schema, forced tenant RLS, audit/idempotency records, forward-only migrations and deterministic Atlas/Delta seed scenarios.
- Plants, product profiles, BOM/formula/routing versions, co-/by-products, substitutions, resources, production snapshots, production lifecycle, material conservation, partial output, scrap, quality gates, labor, subcontract commitments, genealogy, close readiness, planned/actual cost, variance and WIP are implemented.
- MRP refreshes Sales, CRM Won opportunity, Purchasing and Inventory owner capabilities; it nets available stock, recursively explodes dependent demand, preserves pegging and supports planned-supply firming and MPS publication.
- Manufacturing uses typed seams for HR, Quality, Maintenance, Projects and Fixed Assets. It does not write another module's tables. Quality is fail-closed when the owner provider is unavailable.
- Labor and subcontract cost facts are idempotent and project-linked. Manufacturing emits cost events; Ledger remains the semantic posting owner.
- The existing transactional Outbox/RabbitMQ/Worker/Inbox path was verified locally. New labor and subcontract events produced one Inbox record and one balanced Ledger journal per cost fact, with no duplicate financial outcome.
- Authorization checks object legal entity before permission evaluation for orders, plants, operations, work centers, planning runs, MPS plans, outputs and material events. List limits are bounded.

## Final P0 correction evidence

- MRP uses a shared stock-allocation map across roots and recursive children, persists allocated versus uncovered quantities, protects forecast/Sales overlap, and safely rebuilds dependent demands and pegging on rerun. `ManufacturingP0PostgresAcceptanceTests` passed 2/2 against isolated PostgreSQL.
- Quality gate required state is server-authoritative. Execution users cannot downgrade a required gate; only the separate audited policy endpoint can change a Draft-order policy with a non-empty reason, and Passed/Failed remains fail-closed without verified Quality evidence.
- Close transitions reject non-zero unexplained WIP and any missing/unposted/unbalanced Manufacturing Ledger fact. The Ledger read contract is legal-entity scoped and checks the posted journal totals before close.
- Genealogy verifies input/output lots through the Inventory capability and rejects forged, archived, mismatched or unconfirmed lots. Replaying a different request under the same idempotency key returns a conflict.

## Fresh local evidence

- Isolated database: `todir_module26_p0_20261121`; local PostgreSQL, RabbitMQ, API and Worker only.
- DbMigrator applied or verified 240 ordered scripts; Development Seed completed on the isolated database and the disposable Development Seed regression reran it twice successfully.
- Authenticated runtime: labor, subcontract, quality fail-closed, MRP netting, capacity, close-readiness and genealogy forward/reverse routes.
- Manufacturing 6/6; Manufacturing P0 PostgreSQL acceptance 2/2; Security 7/7; Architecture 21/21; SeedData 5/5.
- API and Worker Release builds passed with 0 warnings/errors; the live isolated API returned OpenAPI (1,067 paths, 59 Manufacturing paths) and health HTTP 200. The broader API composition test project is currently blocked by unrelated Navigation/lifecycle test edits in the working tree that are syntactically invalid; those files are preserved and not changed by this Manufacturing correction. Formatter and `git diff --check` remain final delivery gates.

## Deferred / separate gates

- PJ38 heavyweight performance certification/load tests are explicitly deferred by the Product Owner.
- Quality inspection decisions, HR payroll-rate authority, Maintenance downtime truth and Fixed Assets/CIP capitalization remain owner-module integration dependencies behind typed capabilities.
- Frontend implementation, browser evidence and human Product/UX acceptance are separate missions.

## Delivery checkpoint

Backend is ready for frontend handoff. The frontend must use the public API, preserve stable error/idempotency contracts and never write Manufacturing, Inventory or other module tables directly.
