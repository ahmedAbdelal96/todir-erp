# Module 26 Manufacturing / MRP Backend Coverage Matrix

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This is the final local backend/database/integration/development-seed record for MF00–MF47. Frontend implementation and human Product/UX acceptance are separate gates.

## P0 acceptance correction (2026-11-21)

The final correction set closes the three P0 findings from the independent acceptance review:

- MRP now carries one shared stock-allocation ledger through the complete multi-level explosion. Forecast and accepted Sales demand are grossed with overlap protection before stock is allocated; dependent rows persist `stock_allocated_qty` and `uncovered_qty`, and reruns detach/rebuild dependent pegging safely.
- Quality execution requests cannot lower an existing or operation-required gate. A policy change is a separate authorized, reasoned, audited Draft-order operation; result submission remains fail-closed when the Quality owner/evidence is unavailable.
- Completion/close now blocks unexplained positive WIP and requires Manufacturing Ledger facts to be posted, journal-linked and debit/credit balanced. Inventory-owned lot snapshots are required for genealogy output/input identities, with request-hash idempotency conflict detection.

| Capability | Persistence and business rule | API / integration proof | Seed / test proof | Status |
|---|---|---|---|---|
| Plant, profile, BOM, formula and routing | Tenant/entity-scoped master data, versioned definitions, co-/by-product allocation, calendars and resources | Authenticated Manufacturing API routes | Atlas and Delta plants, approved definitions and formula/resource fixtures | PASS |
| Production lifecycle and snapshot | Draft → Released → InProgress → Completed/Closed; released definition is immutable; optimistic versioning | Order, operation, material, output, scrap, quality and close-readiness routes | Seeded partial order with material conservation | PASS |
| Inventory ownership and recovery | Required/reserved/issued/consumed/returned/scrapped remain distinct; Inventory owns stock movement and valuation | Typed Inventory capabilities plus source-key recovery and idempotency | 100 issued = 60 consumed + 40 returned; output source validation | PASS |
| Actual cost, WIP, variance and Ledger | Immutable labor/subcontract facts; Manufacturing emits `ProductionCostFactRecordedV1`; Ledger posts WIP/accrual or variance | Outbox → RabbitMQ → Worker → Inbox → Ledger | Fresh local labor event posted once through the real broker path; journal debit 10 / credit 10; close read requires posted balanced facts | PASS |
| Labor and subcontract costing | Employee/entity validation, positive commercial source validation, request-hash conflict/replay protection | HR employee capability and Purchasing PO capability; Projects cost integration | Labor 201/replay protection; subcontract 201 and changed-key 409; Projects link verified | PASS |
| Quality ownership | Client payload cannot self-authorize a Passed/Failed decision; unavailable provider fails closed and blocks execution | Quality capability seam and quality-gate routes | Forged decision rejected with `manufacturing.quality_provider_unavailable`; blocked close/release guard verified | PASS (fail-closed seam; provider deferred) |
| MRP and MPS | Owner-source demand/supply refresh, single authoritative stock netting, overlap-safe forecast grossing, recursive phantom explosion, pegging, planned supply and MPS publication | Sales/CRM/Purchasing/Inventory capability reads; planning and MPS routes | PostgreSQL acceptance proves `gross = stock_allocated + uncovered`, no duplicate dependent stock allocation, idempotent rerun and finished-stock netting | PASS |
| Capacity and downtime seam | Calendar timezone conversion, efficiency, bounded date range and maintenance downtime ownership | Capacity endpoint and Maintenance capability seam | Authenticated capacity returned 6.3000 available hours and `Ready` for seeded work center | PASS |
| Genealogy and partial output | Per-report links retain input/output quantities and certainty; forward and reverse reads are tenant/entity scoped and idempotent | Create, forward and reverse genealogy routes | Fresh local link created once and returned from both directions; duplicate key replays existing link | PASS |
| Close and financial controls | Close-readiness exposes material, output, operation, quality, cost and WIP blockers; posted facts remain immutable | Close-readiness, cost and variance routes | Seeded order returned `Blocked` with `manufacturing.output_incomplete` and reconciled material quantities | PASS |
| Tenant, legal-entity and authorization scope | Missing tenant context denies access; object-specific legal entity is checked before permission; forced RLS protects module tables | Permission-aware routes and entity-scoped authorization closures | Security 7/7; Manufacturing route checks and RLS probe passed | PASS |
| Outbox / Worker / Inbox | Existing transactional messaging path, inbox idempotency and audit, no duplicate financial outcome | Local RabbitMQ and `manufacturing-integration` consumer | Event `01a1fde7-26b2-76af-a68b-134968198c54` was Published once, processed once in Inbox, and posted once in Ledger with balanced journal `01a1fde7-3ba2-71fc-817b-8185e7b2af25` | PASS |
| Migration and Development Seed | Forward-only ordered migrations and deterministic rerunnable seed | DbMigrator against isolated PostgreSQL | 239 ordered scripts verified; seed completed twice; Atlas/Delta Ledger reports 74 accounts and invoice rule ready | PASS |

## Final local evidence

- Manufacturing 6/6; Manufacturing P0 PostgreSQL acceptance 2/2; Security 7/7; Architecture 21/21; SeedData 5/5.
- API and Worker builds completed with 0 warnings and 0 errors; isolated DbMigrator applied or verified 240 ordered scripts, including the forward-only P0 acceptance correction.
- Development Seed completed on `todir_module26_p0_20261121`; the disposable SeedData regression reran migration/seed twice and passed all 5 checks. Atlas and Delta each reported 2 legal entities, 74 Ledger accounts, 24 periods and invoice rule ready.
- Real local API, PostgreSQL, RabbitMQ and Worker evidence includes the cost Outbox → RabbitMQ → Worker → Inbox → Ledger path, balanced posting, MRP stock netting/rerun behavior, close-readiness guards and genealogy ownership validation.
- OpenAPI and health endpoints, formatter verification, `git diff --check`, Release build and local runtime gates are final delivery checks.

## MVP, deferred and boundary decisions

- PJ38 heavyweight performance certification/load testing is explicitly deferred by the Product Owner; normal bounded queries, indexes and pagination remain in scope.
- Quality inspection decisions remain Quality-owned. Module 26 provides the typed capability and fail-closed behavior; a production Quality provider is a separate integration dependency.
- HR validates employee and legal-entity ownership. The labor command's explicit Manufacturing rate is used until an approved HR payroll-rate capability is available; no fabricated HR rate is inferred.
- Fixed-asset/CIP and Maintenance integrations remain capability seams; Manufacturing does not write those module tables directly.
- Frontend screens, browser UX evidence and human Product/UX acceptance are separate missions.
