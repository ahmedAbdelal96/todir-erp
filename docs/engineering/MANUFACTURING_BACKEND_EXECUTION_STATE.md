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

## Fresh local evidence

- Isolated database: `todir_module26_audit_20261121`; local PostgreSQL, RabbitMQ, API and Worker only.
- DbMigrator rerun: 239 ordered scripts applied or verified; Development Seed rerun twice, with Atlas and Delta each reporting 2 legal entities, 74 Ledger accounts, 24 periods and invoice rule ready.
- Authenticated runtime: labor, subcontract, quality fail-closed, MRP netting, capacity, close-readiness and genealogy forward/reverse routes.
- Manufacturing 6/6; Sales 2/2; Purchasing 10/10; Service 5/5; Projects 22/22; Security 7/7; Architecture 21/21; SeedData 5/5.
- API, Worker and DbMigrator builds passed with 0 warnings/errors. Release build, OpenAPI/health, formatter and `git diff --check` are final gates.

## Deferred / separate gates

- PJ38 heavyweight performance certification/load tests are explicitly deferred by the Product Owner.
- Quality inspection decisions, HR payroll-rate authority, Maintenance downtime truth and Fixed Assets/CIP capitalization remain owner-module integration dependencies behind typed capabilities.
- Frontend implementation, browser evidence and human Product/UX acceptance are separate missions.

## Delivery checkpoint

Backend is ready for frontend handoff. The frontend must use the public API, preserve stable error/idempotency contracts and never write Manufacturing, Inventory or other module tables directly.
