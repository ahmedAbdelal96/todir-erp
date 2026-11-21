# Module 26 Manufacturing / MRP Backend Coverage Matrix

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This is the final local backend/database/integration/development-seed record for MF00–MF47. Frontend implementation and human Product/UX acceptance are separate gates.

| Capability | Persistence and business rule | API / integration proof | Seed / test proof | Status |
|---|---|---|---|---|
| Plant, profile, BOM, formula and routing | Tenant/entity-scoped master data, versioned definitions, co-/by-product allocation, calendars and resources | Authenticated Manufacturing API routes | Atlas and Delta plants, approved definitions and formula/resource fixtures | PASS |
| Production lifecycle and snapshot | Draft → Released → InProgress → Completed/Closed; released definition is immutable; optimistic versioning | Order, operation, material, output, scrap, quality and close-readiness routes | Seeded partial order with material conservation | PASS |
| Inventory ownership and recovery | Required/reserved/issued/consumed/returned/scrapped remain distinct; Inventory owns stock movement and valuation | Typed Inventory capabilities plus source-key recovery and idempotency | 100 issued = 60 consumed + 40 returned; output source validation | PASS |
| Actual cost, WIP, variance and Ledger | Immutable labor/subcontract facts; Manufacturing emits `ProductionCostFactRecordedV1`; Ledger posts WIP/accrual or variance | Outbox → RabbitMQ → Worker → Inbox → Ledger | Fresh local labor and subcontract facts posted balanced debit 140000 / credit 240000 journals | PASS |
| Labor and subcontract costing | Employee/entity validation, positive commercial source validation, request-hash conflict/replay protection | HR employee capability and Purchasing PO capability; Projects cost integration | Labor 201/replay protection; subcontract 201 and changed-key 409; Projects link verified | PASS |
| Quality ownership | Client payload cannot self-authorize a Passed/Failed decision; unavailable provider fails closed and blocks execution | Quality capability seam and quality-gate routes | Forged decision rejected with `manufacturing.quality_provider_unavailable`; blocked close/release guard verified | PASS (fail-closed seam; provider deferred) |
| MRP and MPS | Owner-source demand/supply refresh, inventory netting, recursive phantom explosion, pegging, planned supply and MPS publication | Sales/CRM/Purchasing/Inventory capability reads; planning and MPS routes | Authenticated run persisted owner demands and dependent demand; finished stock netted before planned production | PASS |
| Capacity and downtime seam | Calendar timezone conversion, efficiency, bounded date range and maintenance downtime ownership | Capacity endpoint and Maintenance capability seam | Authenticated capacity returned 6.3000 available hours and `Ready` for seeded work center | PASS |
| Genealogy and partial output | Per-report links retain input/output quantities and certainty; forward and reverse reads are tenant/entity scoped and idempotent | Create, forward and reverse genealogy routes | Fresh local link created once and returned from both directions; duplicate key replays existing link | PASS |
| Close and financial controls | Close-readiness exposes material, output, operation, quality, cost and WIP blockers; posted facts remain immutable | Close-readiness, cost and variance routes | Seeded order returned `Blocked` with `manufacturing.output_incomplete` and reconciled material quantities | PASS |
| Tenant, legal-entity and authorization scope | Missing tenant context denies access; object-specific legal entity is checked before permission; forced RLS protects module tables | Permission-aware routes and entity-scoped authorization closures | Security 7/7; Manufacturing route checks and RLS probe passed | PASS |
| Outbox / Worker / Inbox | Existing transactional messaging path, inbox idempotency and audit, no duplicate financial outcome | Local RabbitMQ and `manufacturing-integration` consumer | New cost events were published, consumed once, persisted in Inbox, and posted once in Ledger | PASS |
| Migration and Development Seed | Forward-only ordered migrations and deterministic rerunnable seed | DbMigrator against isolated PostgreSQL | 239 ordered scripts verified; seed completed twice; Atlas/Delta Ledger reports 74 accounts and invoice rule ready | PASS |

## Final local evidence

- Manufacturing 6/6; Sales 2/2; Purchasing 10/10; Service 5/5; Projects 22/22; Security 7/7; Architecture 21/21; SeedData 5/5.
- API, Worker and DbMigrator builds completed with 0 warnings and 0 errors; isolated DbMigrator verified 239 scripts.
- Development Seed completed twice on `todir_module26_audit_20261121`: Atlas and Delta each reported 2 legal entities, 74 Ledger accounts, 24 periods and invoice rule ready.
- Real local API, PostgreSQL, RabbitMQ and Worker evidence includes labor/subcontract cost posting, balanced WIP/accrual journals, quality fail-closed behavior, MRP netting, capacity, close-readiness and bidirectional genealogy.
- OpenAPI and health endpoints, formatter verification, `git diff --check`, Release build and local runtime gates are final delivery checks.

## MVP, deferred and boundary decisions

- PJ38 heavyweight performance certification/load testing is explicitly deferred by the Product Owner; normal bounded queries, indexes and pagination remain in scope.
- Quality inspection decisions remain Quality-owned. Module 26 provides the typed capability and fail-closed behavior; a production Quality provider is a separate integration dependency.
- HR validates employee and legal-entity ownership. The labor command's explicit Manufacturing rate is used until an approved HR payroll-rate capability is available; no fabricated HR rate is inferred.
- Fixed-asset/CIP and Maintenance integrations remain capability seams; Manufacturing does not write those module tables directly.
- Frontend screens, browser UX evidence and human Product/UX acceptance are separate missions.
