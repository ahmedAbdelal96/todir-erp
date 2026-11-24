# Module 27 Quality Management — Execution State

## Scope

Backend, database, integrations, Development Seed and local acceptance were implemented against the canonical Module 27 pack. The module owns inspection decisions, evidence, restrictions/release reasons, disposition, quality issues/NCR/CAPA, supplier-quality evidence and certificates. Inventory, Catalog, Purchasing, Manufacturing, Maintenance, Sales, Ledger and fixed-asset identities remain owned by their modules.

## Completed

- Added the `business_quality` baseline with tenant-owned tables, constraints, workload indexes, RLS and `FORCE ROW LEVEL SECURITY`; the durable release-operation table is forward-only and owner execution fields are additive.
- Added Quality module manifest, shared decision/receiving contracts, repository, API routes and provider registration in API/Worker.
- Implemented plan/version/applicability resolution, characteristic/specification snapshots, Full/Fixed/Percentage sampling, inspection results and immutable completed evaluations.
- Implemented reinspection history, QualityHold/Quarantine/Blocked/ReturnsInspection restrictions, release reasons, disposition, deviation/concession storage, issues, NCR, containment, CAPA and effectiveness checks.
- Added supplier quality profile/SCAR and COA/CoC issuance requiring a Passed inspection.
- Added pre-receipt Quality resolution to Purchasing; mandatory lines are handed to Inventory with `QualityHold` before the receipt is committed.
- Manufacturing already had the fail-closed provider seam; Quality now supplies the authoritative provider from persisted evaluation evidence.
- Added a Worker `quality-integration` consumer with Inbox idempotency for `StockReceivedV1`, production completion/output, sales-return and calibration status events; processed events are persisted in `quality_integration_event`.
- Added deterministic Atlas and Delta seed records and a focused six-test Quality domain suite.
- Added durable partial-release operations with idempotency/request-hash conflict protection, in-flight quantity reservation, Unknown-state retry and owner-operation linkage.
- Added owner-backed Scrap, Supplier Return and Manufacturing Rework capabilities through Inventory, Purchasing and Manufacturing contracts; Quality remains the decision owner and each side effect is persisted by its owning module.
- Closed the targeted safety gap in Supplier Return: Quality keeps the restriction active while Purchasing creates/approves the return, Inventory issues directly from `QualityHold`, and only a confirmed owner operation allows Quality to resolve the restriction. No pre-return `Available` release operation is created.
- Added deterministic disposition reconciliation through the persisted owner idempotency key. `Unknown` can be retried through the authorized reconcile route; successful owner replays are idempotent, while deterministic owner failures remain `Failed` and non-Executed.
- Added bounded typed list/read APIs for the operational work queues and master-data selectors, including sampling plans, characteristics, restrictions, dispositions, issues, NCRs, CAPA, supplier profiles, SCARs and certificates.

## Fresh evidence

- `dotnet build backend/Api/Api.csproj --configuration Release --no-restore`: PASS, 0 warnings/errors.
- `dotnet build backend/Worker/Worker.csproj --configuration Release --no-restore`: PASS, 0 warnings/errors.
- `dotnet build backend/DbMigrator/DbMigrator.csproj --configuration Release --no-restore`: PASS, 0 warnings/errors.
- `dotnet test backend/Tests/Modules/Quality/QualityTests.csproj --configuration Release --no-restore`: PASS, 6/6.
- `dotnet test backend/Tests/Modules/Manufacturing/ManufacturingTests.csproj --configuration Release --no-restore`: PASS, 16/16.
- `dotnet test backend/Tests/Modules/Purchasing/PurchasingTests.csproj --configuration Release --no-restore`: PASS, 10/10.
- `dotnet test backend/Tests/Modules/Inventory/InventoryTests.csproj --configuration Release --no-restore` with an isolated migrated/seeded PostgreSQL database: PASS, 12/12.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --configuration Release --no-restore`: PASS, 23/23.
- `dotnet test backend/Tests/SeedData/SeedDataTests.csproj --configuration Release --no-restore` with local admin/runtime connections: PASS, 5/5.
- `dotnet test backend/Tests/Security/SecurityTests.csproj --configuration Release --no-restore` with local admin/runtime connections: PASS, 7/7.
- Authenticated local API smoke test: login 200 and `GET /api/v1/quality/plans` 200 for Atlas.
- Real local RabbitMQ/Worker path: the disposable acceptance published `StockReceivedV1`, observed `quality_integration_event=Applied`, one integration-created inspection, one processed `quality-integration` Inbox row, and no duplicate after replay.
- Local DbMigrator applied/verified 257 ordered platform scripts (256 ordered module scripts plus final RLS finalization, including the disposition/release-operation, Purchasing return-idempotency and operational-index fixes) and seeded both Atlas and Delta; output included 1 plan, 1 inspection, 1 restriction, 1 NCR and 1 CAPA per tenant.
- Real PostgreSQL check as `erp_app`: no tenant context returned 0 Quality plans; Atlas and Delta contexts each returned only their own seeded plan. `quality_plan`, `inspection` and `quality_restriction` reported RLS enabled and forced.

## Final acceptance evidence — 2026-11-23

- Added source-fingerprint uniqueness/idempotent inspection creation, typed keyset pagination, explicit effective-plan validation/conflict detection, calibration/sample/UOM hardening, owner-operation claiming/recovery, immutable certificate snapshots, typed Quality reads, CAPA action/root-cause lifecycle and SCAR response/review lifecycle.
- `QualityBackendAcceptanceTests` passed 11/11 against fresh isolated local PostgreSQL database `todir_quality_m27_final` after the final source and migration changes. QA-01 proves receiving resolution; QA-03 proves Manufacturing Rework owner evidence and blocked continuation; QA-04 proves replay/concurrent partial-release conservation; QA-08 proves failed/Unknown owner recovery and exactly-once owner operation; QA-09 proves RLS; QA-10 proves source-fingerprint idempotency and the real RabbitMQ Worker Inbox path.

## Targeted safety closure evidence — 2026-11-23

- Fresh disposable `todir_quality_targeted`: DbMigrator applied/verified 257 ordered scripts and Development Seed completed for Atlas and Delta.
- `QualityBackendAcceptanceTests`: PASS 15/15 after the final source change. The original 11 acceptance scenarios remain present; QA-11 proves the actual Purchasing receipt service creates mandatory QualityHold, QA-12 proves Return consumes QualityHold without an Available release and without duplicate return/movement rows, QA-13 proves Unknown reconciliation reuses the owner identity exactly once, and QA-14 proves a deterministic Purchasing failure leaves the restriction Active and the quantity restricted.
- Authenticated lifecycle regression with the restricted non-admin operator on a disposable dedicated database: PASS 1/1. Quality mutation was denied with `module.lifecycle_readonly`, `module.disabled`, and `module.unentitled` for ReadOnly, Disabled, and removed entitlement states.
- No new database migration was required; the existing 257-script schema already contains the disposition owner state, idempotency and supplier-return link structures. The code path was verified against a fresh migration/seed rather than the protected development database.

## Known boundary

The full UI/product acceptance is a separate frontend mission. Advanced Build Later Quality features are explicitly deferred by the Product Owner and are not hidden behind the Build Now APIs.

## Correction checkpoint — 2026-11-23

Implemented and locally built the final operational slice: typed Inventory/Purchasing/Manufacturing owner capabilities, durable release operations, persisted disposition owner execution state, per-line receiving resolution, fail-closed inspection/result validation, disposition approval plus owner-linked execution, bounded Quality lists, Quality lifecycle fallback enforcement, real event-consequence inspection creation for applicable inbound events, and seed reference repair. The forward fixes were applied to the fresh `todir_quality_m27_final` database, including the Purchasing migration ordering correction required for clean bootstrap.

Evidence after the correction: targeted builds passed with zero errors; the final fresh migration/seed completed with 257 scripts; Atlas and Delta seed completed; PostgreSQL checks report zero invalid Quality product or plan-version references and zero active owner-pending seeded restrictions; the isolated 11/11 QA suite and real Worker replay are complete.

## Release-blocker closure evidence — 2026-11-25

- `QualityBackendAcceptanceTests`: PASS, 27/27 against the disposable local PostgreSQL acceptance database, including QA-25 full `product_uom` value restoration plus reversal of any test-seeded stock, QA-26 valid authoritative nullable stock identities, and QA-27 explicit administrative/physical hold separation with zero Inventory side effects for documentary holds.
- Real local browser/API stack: PASS, 20/20 Playwright scenarios after the final selector and certificate-identity corrections. Scenario 10 created an administrative hold linked to a product-bearing inspection and reconciled unchanged Inventory balances; Scenarios 11–12 targeted the exact created hold/disposition identity and proved partial/full release conservation; Scenario 16 verified a unique certificate reference, historical inspection identity, and immutable snapshot detail.
- `SeedDataTests`: PASS, 5/5 on a fresh disposable database. DbMigrator now orders the OpenIddict table foundation before OAuth scope data and temporarily relaxes FORCE RLS for Quality disposition foreign-key validation before restoring it.
- Full local gates after the final source changes: ArchitectureTests 23/23, SecurityTests 7/7, frontend unit suite 293/293, frontend typecheck, Vite build, module/database/OpenAPI/release-gate validators, and backend Release solution build all passed. The repository-wide formatter reports only the pre-existing six Ledger whitespace findings; the changed Quality and DbMigrator projects are formatter-clean. `git diff --check` remains the final delivery check.
