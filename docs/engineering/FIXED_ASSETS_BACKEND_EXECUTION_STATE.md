# Fixed Assets backend execution checkpoint

Status: NOT COMPLETE

Canonical pack: `docs/modules/22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Authorized scope is the backend/database mission for Module 22. The current implementation establishes the module boundary, `business_assets` schema, RLS, categories, registry, assignments, book definitions, asset books, deterministic straight-line calculation, calculated and posted depreciation runs with closed-period protection and typed reversal, capitalization candidates and review/allocation controls, retry-safe source allocation with additive correction history and asset-book/Ledger impact, CIP lifecycle and cost sources, book-value reconstruction at disposal with Ledger posting, explicit capital cost additions/reductions with idempotency and Ledger posting, migration/seed integration, API composition, focused domain tests, and the first Ledger semantic-fact slices.

Verified after the latest source change:

- `dotnet build backend/Api/Api.csproj --no-restore` — PASS.
- `dotnet build backend/DbMigrator/DbMigrator.csproj` — PASS.
- `dotnet test backend/Tests/Modules/FixedAssets/FixedAssetsTests.csproj --no-restore` — PASS, 3/3.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --no-restore` — PASS, 21/21.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --no-restore --filter FullyQualifiedName~FixedAssetsDepreciationIntegrationTests` — PASS, 7/7 against the live PostgreSQL database and authenticated API composition. The proof covers administrator authorization, preparation, natural period-conflict idempotency, closed-period depreciation rejection, depreciation Ledger fact posting and typed reversal, opening import validation/posting/retry with 1,000 cost − 400 accumulated depreciation = 600 NBV and `OpeningMigration` provenance, authorized book-balance projection returning the same 1,000/400/600 values, capitalization candidate review/allocation plus same-payload allocation retry and additive allocation correction/retry, source allocation updating asset-book cost and posting acquisition/reduction Ledger facts, CIP cost accumulation, capitalization without an automatic placed-in-service date, disposal book-value reconstruction, disposal Ledger fact linkage, explicit capital cost addition/reduction, idempotent cost retry, immutable asset-book transaction fact linkage, and balanced posted journal lines.
- DbMigrator fresh application of `fixed-assets-schema.sql` on the local PostgreSQL instance — PASS.
- Development seed initial run and idempotent rerun for Atlas and Delta — PASS; each tenant has 5 categories including CIP, 2 assets, 1 Corporate Book, Fixed Assets Ledger roles/mappings/rules, and administrator Fixed Assets permissions.
- `fixed-assets-forward-fix-cip-lifecycle.sql` applied successfully; the live constraint now permits `UnderConstruction` while preserving the migration checksum of the original schema baseline.
- `fixed-assets-forward-fix-source-allocation-corrections.sql` applied successfully; allocation corrections are stored as immutable additive events with tenant RLS.
- `fixed-assets-forward-fix-opening-import.sql` applied successfully; opening lines now carry category/book/policy inputs with foreign-key validation and tenant RLS preserved.
- Runtime-role RLS smoke proof — PASS: missing tenant context returns zero rows; a tenant context returns only that tenant's rows; all 21 Assets tables are enabled and forced RLS.
- FA37 large-scale benchmarks — NOT EXECUTED by Product Owner directive.

Unfinished requirements / next executable work:

- Ledger semantic-fact integration is partially implemented for depreciation, disposal, and explicit capital-cost posting; capitalization posting remains uncalled from Fixed Assets. Broader recovery beyond a single posted depreciation-run reversal is not implemented.
- Source-module candidate ingestion contracts, source allocation reclassification, CIP split/many-to-one capitalization, capital-cost correction/reversal, depreciation recovery beyond a single run reversal, opening-balance-to-GL reconciliation, verification campaigns, bulk operations, reports/reconciliation, document/file contracts, HR offboarding contract, tax-provider seam, and full audit/timeline APIs remain.
- Schedule projections, multi-asset book balance projections, estimate changes, impairment/revaluation seams, grouped split/partial disposal, and gain/loss reconstruction need completion.
- Cross-module integration, API authorization, self-service IDOR, field redaction, concurrency, migration-upgrade, and real API acceptance suites remain to be written.
- The foundation milestone is committed and pushed as `12a41dc`; the Ledger/depreciation milestone is committed and pushed as `39ed60b`; the candidate/CIP milestone is committed and pushed as `ecd62e8`; the disposal-posting milestone is committed and pushed as `508c34c`; the capital-cost posting milestone is committed and pushed as `77e5b61`; source-allocation retry/correction and asset-book/Ledger impact milestones are committed and pushed as `611f5d2`, `0ff3c3e`, and `807c653`; typed depreciation reversal is committed and pushed as `50b5cdb`.

Last meaningful change: added validated/postable opening imports with provenance, opening book transactions, reference checks, and live PostgreSQL proof of opening cost, reserve, and NBV.

Next executable action: commit/push this verified book-balance projection milestone, then implement Ledger/control-account reconciliation read models before extending the remaining lifecycle workflows.
