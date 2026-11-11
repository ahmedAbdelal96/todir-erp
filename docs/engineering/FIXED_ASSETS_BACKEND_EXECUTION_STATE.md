# Fixed Assets backend execution checkpoint

Status: NOT COMPLETE

Canonical pack: `docs/modules/22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Authorized scope is the backend/database mission for Module 22. The current implementation establishes the module boundary, `business_assets` schema, RLS, categories, registry, assignments, book definitions, asset books, deterministic straight-line calculation, calculated and posted depreciation runs with closed-period protection and typed reversal, capitalization candidates and review/allocation controls, retry-safe source allocation with additive correction history and asset-book/Ledger impact, CIP lifecycle and cost sources, book-value reconstruction at disposal with Ledger posting, explicit capital cost additions/reductions with idempotency and Ledger posting, migration/seed integration, API composition, focused domain tests, and the first Ledger semantic-fact slices. The latest increments add prospective estimate-change persistence/calculation, current/as-of assignment reads, resource/list authorization hardening, and permissioned file-reference metadata plus asset-tag/QR scan reads.

Verified after the latest source change:

- `dotnet build backend/Api/Api.csproj --no-restore` — PASS.
- `dotnet build backend/DbMigrator/DbMigrator.csproj` — PASS.
- `dotnet test backend/Tests/Modules/FixedAssets/FixedAssetsTests.csproj --no-restore` — PASS, 3/3.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --no-restore` — PASS, 21/21.
- `dotnet test backend/Tests/Modules/Ledger/LedgerTests.csproj --no-restore` with the live local runtime role (`erp_app`) — PASS, 55/55. The earlier 53/55 result was a test-environment defect: the suite was run as `postgres`, so forced RLS was bypassed and the deliberately invalid cross-tenant audit insert succeeded.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --no-restore --filter FullyQualifiedName~FixedAssetsDepreciationIntegrationTests` — PASS, 8/8 against the live PostgreSQL database and authenticated API composition. The proof covers administrator authorization, preparation, natural period-conflict idempotency, closed-period depreciation rejection, depreciation Ledger fact posting and typed reversal, opening import validation/posting/retry with 1,000 cost − 400 accumulated depreciation = 600 NBV and `OpeningMigration` provenance, authorized book-balance projection returning the same 1,000/400/600 values, Ledger-backed book reconciliation returning an explicit `Healthy`/`Difference` result, capitalization candidate review/allocation plus same-payload allocation retry and additive allocation correction/retry, source allocation updating asset-book cost and posting acquisition/reduction Ledger facts, CIP cost accumulation, capitalization without an automatic placed-in-service date, disposal book-value reconstruction, disposal Ledger fact linkage, explicit capital cost addition/reduction, idempotent cost retry, immutable asset-book transaction fact linkage, balanced posted journal lines, and a prospective estimate-change/idempotency/depreciation-basis vector.
- DbMigrator fresh application of `fixed-assets-schema.sql` on the local PostgreSQL instance — PASS.
- Development seed initial run and idempotent rerun for Atlas and Delta — PASS; each tenant has 8 governed categories including CIP, 4 representative assets, 3 books, Fixed Assets Ledger roles/mappings/rules, and administrator Fixed Assets permissions.
- `fixed-assets-forward-fix-cip-lifecycle.sql` applied successfully; the live constraint now permits `UnderConstruction` while preserving the migration checksum of the original schema baseline.
- `fixed-assets-forward-fix-source-allocation-corrections.sql` applied successfully; allocation corrections are stored as immutable additive events with tenant RLS.
- `fixed-assets-forward-fix-opening-import.sql` applied successfully; opening lines now carry category/book/policy inputs with foreign-key validation and tenant RLS preserved.
- Runtime-role RLS smoke proof — PASS: missing tenant context returns zero rows; a tenant context returns only that tenant's rows; all 21 Assets tables are enabled and forced RLS.
- Platform numbering integration — asset creation, CIP creation, and opening-import creation now allocate tenant/legal-entity scoped numbers through the Platform sequence capability (`fixed-assets.asset-number`), rather than generating identifiers in the Assets module.
- Resource authorization hardening — resource-scoped mutation routes resolve the persisted legal entity before permission evaluation for assets, books, depreciation runs, capitalization candidates, and opening imports; API integration remains PASS, 7/7.
- Opening-to-Ledger linkage — opening imports for Ledger-posting books now create balanced `OpeningBalancePosted` Ledger facts from Fixed Asset control mappings, persist the fact reference on the immutable opening transaction, and verify the posted fact in API acceptance; Fixed Assets integration remains PASS, 7/7.
- Fixed Asset event contracts — `FixedAssetCreatedV1`, `FixedAssetAssignedV1`, `FixedAssetCapitalizedV1`, `FixedAssetDepreciatedV1`, and `FixedAssetDisposedV1` are typed shared contracts and are queued through the transactional Outbox at their owning lifecycle transitions.
- Development seed expansion — Atlas and Delta now receive 8 governed categories (including Vehicle, Machine, Intangible, and CIP), 4 representative assets, and 3 books; migrator seed rerun is PASS and idempotent.
- Physical operations slice — legal-entity-scoped asset locations, parent-location validation, assignment location/entity validation, and immutable handover records are now exposed through authenticated API routes with resource-scoped authorization; the existing live Fixed Assets integration remains PASS, 7/7 after this change.
- Physical verification slice — campaigns snapshot expected locations without mutating assignments; verification results are immutable line outcomes, and campaign closure rejects unresolved pending lines.
- Asset timeline read slice — `GET /api/v1/fixed-assets/assets/{assetId}/timeline` is resource-authorized and returns bounded, tenant-scoped lifecycle events from asset creation, assignments, handovers, book transactions, disposals, and asset audit events without returning book amounts; opening-import API acceptance verifies creation and opening transaction timeline events.
- Verification campaign read slice — `GET /api/v1/fixed-assets/verification/campaigns` now provides bounded tenant/entity/status filtering with line and pending counts.
- Depreciation policy guard — unsupported `DecliningBalance` is no longer accepted through new category/book/opening configuration paths until a validated method and golden vectors are implemented; straight-line/no-depreciation behavior remains covered by the live 7/7 integration gate.
- Estimate-change slice — `asset_book_estimate_change`, the idempotent `POST /assets/{assetId}/books/{bookId}/estimate-change` contract, and prospective straight-line calculation-basis snapshots are implemented. A dedicated acceptance vector for a changed useful life/residual value is still required.
- Assignment projection slice — `GET /assets/{assetId}/assignments` provides immutable history and current/as-of filtering under resource-scoped authorization.
- Documents and scan slice — ready Platform File references can be attached with role/metadata restrictions, and authenticated asset-tag/QR scan reads expose only physical identity fields; dedicated file-capability integration proof is still required.
- Supplier invoice source slice — `POST /capitalization-candidates/from-invoice` consumes an immutable Invoicing compliance snapshot, rejects non-payable/cross-entity sources, derives the eligible net amount from the snapshot, and reuses the candidate uniqueness boundary; dedicated end-to-end invoice fixture proof is still required.
- Authorization slice — list routes now scope by tenant or supplied legal entity, while mutation and financial reads resolve persisted resource ownership before authorization; the latest live Fixed Assets integration remains PASS, 7/7.
- FA37 large-scale benchmarks — NOT EXECUTED by Product Owner directive.

Unfinished requirements / next executable work:

- Ledger semantic-fact integration is partially implemented for depreciation, disposal, opening migration, and explicit capital-cost posting; capitalization posting remains uncalled from Fixed Assets. Book reconciliation now consumes the Ledger control-balance contract, but broader recovery beyond a single posted depreciation-run reversal is not implemented.
- Source-module candidate ingestion from an immutable finalized Invoicing compliance snapshot is now available through `POST /capitalization-candidates/from-invoice`; Purchasing/Inventory/Expenses source orchestration, source allocation reclassification, CIP split/many-to-one capitalization, capital-cost correction/reversal, depreciation recovery beyond a single run reversal, impairment/revaluation seams, grouped split/partial disposal/reinstatement, asset-sale linkage, HR offboarding gates, tax-provider seam, bulk operations, and full report/export/drilldown APIs remain.
- Location/handover history, current/as-of reads, and a public `IFixedAssetCustodyCapabilities` employee-assets query are implemented, but transfer/return workflow orchestration and HR offboarding completion remain.
- Verification campaign creation/close is implemented; wrong-location resolution, scan evidence, and acceptance coverage remain.
- Cross-module integration, field/document restriction tests, concurrency, migration-upgrade, OpenAPI compatibility, and real API acceptance suites remain to be written.
- The foundation milestone is committed and pushed as `12a41dc`; the Ledger/depreciation milestone is committed and pushed as `39ed60b`; the candidate/CIP milestone is committed and pushed as `ecd62e8`; the disposal-posting milestone is committed and pushed as `508c34c`; the capital-cost posting milestone is committed and pushed as `77e5b61`; source-allocation retry/correction and asset-book/Ledger impact milestones are committed and pushed as `611f5d2`, `0ff3c3e`, and `807c653`; typed depreciation reversal is committed as `50b5cdb`; opening import is committed as `9d5e9a2`; book balance projection is committed as `201a69f`; Ledger-backed reconciliation is committed as `8ed3aba`; numbering/auth/opening Ledger linkage is committed and pushed as `7e54fb2`; locations/handover as `793ce17`; verification campaigns as `b350dc4`; unsupported-method guard as `aefd4c6`; timeline, estimate changes, assignment reads, security hardening, documents/scan, invoice-source candidate ingestion, and custody capability are committed and pushed as `2aaf07d`.

Last meaningful change: added permissioned asset document references and physical scan/QR reads, an immutable invoice-snapshot candidate source, and the public employee-custody capability; the forward migration/development seed and live Fixed Assets integration gate are fresh and PASS, 8/8.

Next executable action: rerun the affected API/integration verification, add a dedicated estimate/document acceptance vector, then continue source-module ingestion, impairment/revaluation, grouped split/partial disposal, bulk operations, HR offboarding and country-pack contracts before the complete backend release gate and commit/push.

## FA00–FA45 coverage matrix

| Requirement | Status | Current evidence / remaining proof |
|---|---|---|
| FA00 Repository / dependency verification | IMPLEMENTED / VERIFIED | Architecture 21/21; module manifest and shared-contract dependencies inspected. |
| FA01 Module boundary, schema, RLS | IMPLEMENTED / VERIFIED | Fresh schema/migration and runtime-role RLS smoke proof; new document table migration pending dedicated RLS assertion. |
| FA02 Category / behavior policy | IMPLEMENTED / VERIFIED | 8 seeded categories per tenant; unsupported DecliningBalance guard; Fixed Assets tests 3/3. |
| FA03 Canonical asset registry | IMPLEMENTED / VERIFIED | Authenticated create/list and seed registry; integration gate 7/7. |
| FA04 Asset locations | IMPLEMENTED / NOT FULLY VERIFIED | Legal-entity locations and parent validation implemented; dedicated runtime scenario still required. |
| FA05 Assignment / custody / handover | PARTIALLY IMPLEMENTED | Immutable assignment/handover history and as-of read implemented; transfer/return orchestration and HR gate remain. |
| FA06 Product / inventory serial linking | PARTIALLY IMPLEMENTED | Registry references exist; Inventory capability integration and serial provenance proof remain. |
| FA07 Documents / tags / QR | PARTIALLY IMPLEMENTED | Permissioned file-reference metadata and authenticated tag/QR read implemented; file-capability acceptance remains. |
| FA08 Asset book definitions | IMPLEMENTED / VERIFIED | Seeded books and authenticated book APIs; integration gate 7/7. |
| FA09 Book core / opening value | IMPLEMENTED / VERIFIED | Immutable transactions, opening import, balance reconstruction; integration gate 7/7. |
| FA10 Capitalization queue | IMPLEMENTED / VERIFIED | Candidate review/allocation flow and retry/correction proof in 7/7 integration. |
| FA11 Supplier source | PARTIALLY IMPLEMENTED | Finalized Invoicing compliance snapshot can create a deduplicated SupplierInvoice candidate; Purchasing finalized-source linkage acceptance remains. |
| FA12 Inventory internal-use | MISSING | No Inventory internal-use conversion capability is wired. |
| FA13 Many-to-many allocation | PARTIALLY IMPLEMENTED | Candidate allocation supports repeated source/asset allocations; 10-laptop/many-cost acceptance vector remains. |
| FA14 CIP | PARTIALLY IMPLEMENTED | CIP creation, cost sources, and capitalization exist; split/many-to-one remains. |
| FA15 Placed-in-service | IMPLEMENTED / VERIFIED | CIP capitalization controls service dates and no depreciation-before-service; integration gate 7/7. |
| FA16 Straight-line depreciation | IMPLEMENTED / VERIFIED | Straight-line/no-depreciation vectors and live run proof. |
| FA17 Additional methods / conventions | EXPLICITLY DEFERRED | DecliningBalance is rejected until validated golden vectors and policy are approved. |
| FA18 Depreciation run / Ledger | IMPLEMENTED / VERIFIED | Prepare/post/closed-period/reversal and Ledger fact proof in 7/7 integration. |
| FA19 Balance / schedule projections | PARTIALLY IMPLEMENTED | Asset-book balance and reconciliation exist; schedule and multi-asset projection APIs remain. |
| FA20 Cost additions / reductions | IMPLEMENTED / VERIFIED | Idempotent source/cost changes and Ledger linkage in 7/7 integration. |
| FA21 Estimate changes | IMPLEMENTED / NOT FULLY VERIFIED | Persistence/API/prospective calculation basis implemented; dedicated changed-estimate golden vector remains. |
| FA22 Impairment | MISSING | Approved impairment event and carrying-value treatment are not implemented. |
| FA23 Revaluation seam | MISSING | No approved revaluation provider/event seam is wired. |
| FA24 Grouped assets / split | MISSING | Group split and allocation-preserving child creation are not implemented. |
| FA25 Parent / component assets | PARTIALLY IMPLEMENTED | Parent reference validation exists; component lifecycle and reporting remain. |
| FA26 Physical verification | PARTIALLY IMPLEMENTED | Campaign snapshot, immutable outcomes, bounded listing and close guard exist; wrong-location resolution proof remains. |
| FA27 Mass operations | MISSING | No previewed, retry-safe bulk transfer/update command exists. |
| FA28 Disposal / scrap / loss | IMPLEMENTED / VERIFIED | Core disposal types, NBV reconstruction and Ledger disposal proof; variant acceptance remains. |
| FA29 Asset sale integration | MISSING | Sale/invoice/payment/disposal linking is not implemented. |
| FA30 Partial disposal / reinstatement | MISSING | No partial basis split or reinstatement command exists. |
| FA31 HR offboarding | PARTIALLY IMPLEMENTED | Public `IFixedAssetCustodyCapabilities` and employee-assets API provide current custody facts; HR return-warning/gate consumption remains. |
| FA32 Maintenance seam | EXPLICITLY DEFERRED | No Maintenance module exists in authorized scope; canonical AssetId seam remains a documented dependency. |
| FA33 Country / tax book provider | MISSING | No provider contract/defaults are wired into Fixed Assets books. |
| FA34 Reports / reconciliation | PARTIALLY IMPLEMENTED | Book/GL reconciliation exists; operational reports, source drilldown and bounded exports remain. |
| FA35 Opening migration | IMPLEMENTED / VERIFIED | Validated opening import, retry, NBV and Ledger linkage in 7/7 integration. |
| FA36 Security / permissions / audit | PARTIALLY IMPLEMENTED | RLS, resource/list authorization, immutable events and redaction boundaries exist; attack-case suite remains. |
| FA37 Performance / load | EXPLICITLY DEFERRED | NOT EXECUTED — Product Owner directive. |
| FA38 Runbooks / recovery | MISSING | Required semantic recovery runbooks are not yet committed. |
| FA39 Operational acceptance | PARTIALLY IMPLEMENTED | Core identity/custody/verification APIs exist; full laptop/land/vehicle journeys and product review remain. |
| FA40 Accountant acceptance | PARTIALLY IMPLEMENTED | Core capitalization/CIP/depreciation/disposal slices pass; source, split and estimate vectors remain. |
| FA41 HR custody acceptance | MISSING | Offboarding scenario is not implemented. |
| FA42 Finance / GL acceptance | PARTIALLY IMPLEMENTED | Opening/depreciation/disposal/cost Ledger facts and book reconciliation pass; impairment/revaluation/sale coverage remains. |
| FA43 Country / tax acceptance | MISSING | No country-pack Fixed Assets provider acceptance. |
| FA44 Cross-module end-to-end | MISSING | Purchasing/Inventory/Expenses/HR/Sales end-to-end journeys are not wired. |
| FA45 Release gate | NOT COMPLETE | Backend mission has not passed all authorized requirements, fresh full gates, or Product Owner acceptance. |
