# Module 27 Quality Management — Execution State

## Scope

Backend, database, integrations, Development Seed and local acceptance were implemented against the canonical Module 27 pack. The module owns inspection decisions, evidence, restrictions/release reasons, disposition, quality issues/NCR/CAPA, supplier-quality evidence and certificates. Inventory, Catalog, Purchasing, Manufacturing, Maintenance, Sales, Ledger and fixed-asset identities remain owned by their modules.

## Completed

- Added the `business_quality` baseline with 47 tenant-owned tables, constraints, workload indexes, RLS and `FORCE ROW LEVEL SECURITY`.
- Added Quality module manifest, shared decision/receiving contracts, repository, API routes and provider registration in API/Worker.
- Implemented plan/version/applicability resolution, characteristic/specification snapshots, Full/Fixed/Percentage sampling, inspection results and immutable completed evaluations.
- Implemented reinspection history, QualityHold/Quarantine/Blocked/ReturnsInspection restrictions, release reasons, disposition, deviation/concession storage, issues, NCR, containment, CAPA and effectiveness checks.
- Added supplier quality profile/SCAR and COA/CoC issuance requiring a Passed inspection.
- Added pre-receipt Quality resolution to Purchasing; mandatory lines are handed to Inventory with `QualityHold` before the receipt is committed.
- Manufacturing already had the fail-closed provider seam; Quality now supplies the authoritative provider from persisted evaluation evidence.
- Added a Worker `quality-integration` consumer with Inbox idempotency for `StockReceivedV1`, production completion/output, sales-return and calibration status events; processed events are persisted in `quality_integration_event`.
- Added deterministic Atlas and Delta seed records and a focused six-test Quality domain suite.

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
- Real local RabbitMQ/Worker path: a pending `StockReceivedV1` outbox row reached `Published`, the Quality Worker endpoint recorded Inbox processing, and 10 inbound events were persisted in `business_quality.quality_integration_event` with no pending outbox rows remaining in the isolated database.
- Local DbMigrator applied/verified 249 ordered scripts (including the receiving-fingerprint forward fix) and seeded both Atlas and Delta; output included 1 plan, 1 inspection, 1 restriction, 1 NCR and 1 CAPA per tenant.
- Real PostgreSQL check as `erp_app`: no tenant context returned 0 Quality plans; Atlas and Delta contexts each returned only their own seeded plan. `quality_plan`, `inspection` and `quality_restriction` reported RLS enabled and forced.

## Known boundary

The full UI/product acceptance is a separate frontend mission. Advanced Build Later Quality features are explicitly deferred by the Product Owner and are not hidden behind the Build Now APIs.
