# Fixed Assets backend execution checkpoint

Status: NOT COMPLETE

Canonical pack: `docs/modules/22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Authorized scope is the backend/database mission for Module 22. The current implementation establishes the module boundary, `business_assets` schema, RLS, categories, registry, assignments, book definitions, asset books, deterministic straight-line calculation, calculated and posted depreciation runs, capitalization candidates and review/allocation controls, CIP lifecycle and cost sources, book-value reconstruction at disposal with Ledger posting, migration/seed integration, API composition, focused domain tests, and the first Ledger semantic-fact slices.

Verified after the latest source change:

- `dotnet build backend/Api/Api.csproj --no-restore` — PASS.
- `dotnet build backend/DbMigrator/DbMigrator.csproj` — PASS.
- `dotnet test backend/Tests/Modules/FixedAssets/FixedAssetsTests.csproj --no-restore` — PASS, 3/3.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --no-restore` — PASS, 21/21.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --no-restore --filter FullyQualifiedName~FixedAssets` — PASS, 4/4 against the live PostgreSQL database and authenticated API composition. The proof covered administrator authorization, preparation, natural period-conflict idempotency, depreciation Ledger fact posting, immutable asset-book transaction fact linkage, balanced posted journal lines, capitalization candidate review/allocation, CIP cost accumulation, capitalization without an automatic placed-in-service date, disposal book-value reconstruction, disposal Ledger fact linkage, and balanced disposal journal lines.
- DbMigrator fresh application of `fixed-assets-schema.sql` on the local PostgreSQL instance — PASS.
- Development seed initial run and idempotent rerun for Atlas and Delta — PASS; each tenant has 5 categories including CIP, 2 assets, 1 Corporate Book, Fixed Assets Ledger roles/mappings/rules, and administrator Fixed Assets permissions.
- `fixed-assets-forward-fix-cip-lifecycle.sql` applied successfully; the live constraint now permits `UnderConstruction` while preserving the migration checksum of the original schema baseline.
- Runtime-role RLS smoke proof — PASS: missing tenant context returns zero rows; a tenant context returns only that tenant's rows; all 21 Assets tables are enabled and forced RLS.
- FA37 large-scale benchmarks — NOT EXECUTED by Product Owner directive.

Unfinished requirements / next executable work:

- Ledger semantic-fact integration is partially implemented for depreciation and disposal posting; acquisition/capitalization posting remains uncalled from Fixed Assets. Closed-period/reversal recovery is not implemented.
- Source-module candidate ingestion contracts, source allocation correction/retry policy, CIP split/many-to-one capitalization, capital cost additions/reductions outside CIP, opening import, verification campaigns, bulk operations, reports/reconciliation, document/file contracts, HR offboarding contract, tax-provider seam, and full audit/timeline APIs remain.
- Asset book balance projections, estimate changes, impairment/revaluation seams, grouped split/partial disposal, and gain/loss reconstruction need completion.
- Cross-module integration, API authorization, self-service IDOR, field redaction, concurrency, migration-upgrade, and real API acceptance suites remain to be written.
- The foundation milestone is committed and pushed as `12a41dc`; the Ledger/depreciation milestone is committed and pushed as `39ed60b`; the candidate/CIP milestone is committed and pushed as `ecd62e8`; the current disposal-posting milestone is verified and awaiting its commit/push.

Last meaningful change: added book-value reconstruction at disposal, Ledger disposal semantic-fact posting, immutable disposal book transaction fact linkage, and live PostgreSQL integration proof.

Next executable action: commit/push this verified disposal-posting milestone, then implement explicit capital cost additions/reductions and reversal/closed-period controls before extending the remaining lifecycle workflows.
