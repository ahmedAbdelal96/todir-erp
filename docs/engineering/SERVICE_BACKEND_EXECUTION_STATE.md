# Module 24 Service / Maintenance / Field Service Backend Execution State

Canonical pack: `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

## Final checkpoint

- Authorized scope: the complete backend, database, API, worker, development seed, cross-module contracts, tenant/legal-entity isolation, and backend acceptance for the canonical Build Now scope and SV00–SV51 scenarios. Frontend and UX remain a separate mission.
- Branch: `dev`; the development database was not reset. Final acceptance ran against disposable PostgreSQL database `todir_service_acceptance` on local PostgreSQL port `15432`; no populated `todir` data was modified.
- Last relevant implementation: authenticated HTTP resource authorization for Service mutations and resource reads, legal-entity filters on bounded list contracts, assignment-bound offline commands and My Day, real Platform Files verification on report save/accept, cross-entity part/meter/due protection, and the expanded frontend handoff contract. Existing template/version snapshots, typed Measurement checklist validation, immutable accepted reports, Inventory operation reversal, invoice-line allocation, reliability KPI fields, and credential isolation were preserved.
- Migration/seed: 215 ordered scripts applied or verified on a fresh database; Development Seed completed for Atlas and Delta and was rerunnable. The applied baseline checksum for `service-forward-fix-template-snapshot.sql` was preserved; additive billing/report fields use `service-forward-fix-billing-report-metrics.sql`.
- Final prior evidence: Service acceptance `4/4`, Service unit `5/5`, Architecture `21/21`, SeedData `5/5`, fresh migration/seed, forced-RLS inspection, tenant cross-read denial, real Inventory reserve→issue→consume→return→reverse, Invoicing draft/allocation, Projects cost ownership, preventive/meter/SLA/contract, maintenance rounds, supplier recovery, fixed-asset history, report immutability/file reference, template snapshot/checklist validation, offline replay/conflict, and KPI reliability fields.
- Current follow-up implementation evidence: the stale disposable acceptance schema was upgraded through the canonical DbMigrator (215 scripts verified; `service-forward-fix-template-snapshot.sql` applied), then Service operational, cross-module, database-integrity, and authenticated HTTP security acceptance passed `5/5`. The security test uses restricted Atlas/Delta identities, a real Ready Platform File fixture, cross-entity/cross-tenant reads, cost denial, and assignment revocation. The existing report fixture also creates a real `platform.file_record`.
- Current status: `COMPLETE` for the Module 24 backend/database scope and frontend handoff. Frontend, UX, browser, and human Product Owner acceptance remain separate gates.
- Next executable action: none for this backend mission; retain the final release evidence below and hand off the documented contracts to Frontend + UX.

## Evidence

| Gate | Result | Evidence |
|---|---|---|
| Solution compilation | PASS | `dotnet build backend/Backend.sln --no-restore /m:1 /v:q`; zero warnings/errors after the final source change. |
| Service unit/domain tests | PASS | `ServiceTests`: 5/5. |
| Service real-stack acceptance | PASS | `ServiceCrossModuleAcceptanceTests`, `ServiceDatabaseIntegrityAcceptanceTests`, and `ServiceOperationalAcceptanceTests`: 4/4; plus `ServiceSecurityIntegrationTests`: 1/1, for 5/5 focused Service acceptance tests after the follow-up. |
| Architecture boundary | PASS | `ArchitectureTests`: 21/21. |
| Development seed | PASS | `SeedDataTests`: 5/5; Atlas and Delta seed with real Catalog, Inventory, Fixed Assets, Projects and Invoicing references. |
| PostgreSQL migration | PASS | 215 ordered scripts applied/verified; disposable runtime role `erp_app`; development database `todir` was not used. |
| Tenant/RLS boundary | PASS (prior evidence) | Service tables are force-RLS protected; cross-tenant Work Order reads return no record and the acceptance guard rejects `todir` and unsafe identities. Must be recaptured after the follow-up change. |
| Authenticated HTTP resource authorization | PASS | `ServiceSecurityIntegrationTests`: 1/1 against disposable PostgreSQL; restricted Atlas/Delta identities, cross-entity/cross-tenant reads, restricted cost routes, and revoked assignment denial all pass. |
| Platform Files ownership/classification | PASS | `SaveReportAsync` and `AcceptReportAsync` call `IPlatformFileReferenceCapability` for Ready `service/work_order` references and reject forged/unavailable/disallowed references; real fixture and forged HTTP case pass. |
| Cross-module ownership | PASS | Inventory owns stock operations, Invoicing owns invoice drafts, Projects owns project billing cost facts, and Fixed Assets remains the asset-history source boundary. |
| Formatting/whitespace | PASS | `dotnet format backend/Backend.sln --verify-no-changes --no-restore` and `git diff --check`. |
| Performance scope | PASS for normal scope; heavyweight certification deferred | Bounded pagination/indexes and normal query behavior remain the applicable gate. No 100k/1M-row benchmark was run or claimed. |

## Deferred scope

Exactly as documented by the canonical pack: advanced route optimization, continuous GPS, IoT predictive maintenance, advanced spare-parts optimization, full depot-repair logistics, full supplier warranty-claim processing, advanced EHS/fleet, autonomous AI dispatch/warranty, complex global contract revenue recognition, customer portal, and remote AR/IoT diagnostics.
