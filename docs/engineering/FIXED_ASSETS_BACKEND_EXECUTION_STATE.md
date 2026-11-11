# Fixed Assets backend execution checkpoint

Status: NOT COMPLETE

Canonical pack: `docs/modules/22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Authorized scope is the backend/database mission for Module 22. The current implementation establishes the module boundary, `business_assets` schema, RLS, categories, registry, assignments, book definitions, asset books, deterministic straight-line calculation, calculated depreciation runs, disposal records, migration/seed integration, API composition, and focused domain tests.

Verified after the latest source change:

- `dotnet build backend/Api/Api.csproj --no-restore` — PASS.
- `dotnet build backend/DbMigrator/DbMigrator.csproj` — PASS.
- `dotnet test backend/Tests/Modules/FixedAssets/FixedAssetsTests.csproj --no-restore` — PASS, 3/3.
- DbMigrator fresh application of `fixed-assets-schema.sql` on the local PostgreSQL instance — PASS.
- Development seed initial run and idempotent rerun for Atlas and Delta — PASS; each tenant has 4 categories, 2 assets, and 1 Corporate Book.
- Runtime-role RLS smoke proof — PASS: missing tenant context returns zero rows; a tenant context returns only that tenant's rows; all 21 Assets tables are enabled and forced RLS.
- FA37 large-scale benchmarks — NOT EXECUTED by Product Owner directive.

Unfinished requirements / next executable work:

- Ledger semantic-fact integration and closed-period/reversal recovery are not implemented.
- Capitalization candidate commands, source allocation concurrency, CIP capitalization/split, opening import, verification campaigns, bulk operations, reports/reconciliation, document/file contracts, HR offboarding contract, tax-provider seam, and full audit/timeline APIs remain.
- Asset book balance projections, estimate changes, impairment/revaluation seams, grouped split/partial disposal, and gain/loss reconstruction need completion.
- Cross-module integration, API authorization, self-service IDOR, field redaction, concurrency, migration-upgrade, and real API acceptance suites remain to be written.
- No commit or push has been made.

Last meaningful change: added the initial asset-book attachment and calculated straight-line depreciation-run workflow, then rebuilt and reran focused tests.

Next executable action: implement source capitalization/allocation and CIP transactions with idempotent persisted outcomes, then add real PostgreSQL integration coverage before extending the remaining lifecycle workflows.
