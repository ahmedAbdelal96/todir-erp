# Module 19 CRM backend execution state

Canonical pack: `docs/modules/19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md` (v1.0).

The module owns pre-sales leads, opportunities, pipeline/stage history, activities and next actions, seller worklists, forecast metadata, CRM party profiles, buying-committee/contact-role context, interests, custom fields, and the CRM-to-Sales quotation seam. Directory remains the owner of Party identity; Sales owns quotations/orders; Invoicing, Subledger and Payments remain the owners of financial truth. `Pipeline Value != Revenue`.

## Build Now coverage matrix

| Requirement | Database / backend / API | Seed | Automated evidence | Status |
| --- | --- | --- | --- | --- |
| Lead inbox, capture, duplicate detection, assignment, qualification, conversion, merge/import/export | `business_crm.lead`, conversion uniqueness, owner/team visibility, bounded `CrmPage<T>` lists, `/leads/*` commands | 40 deterministic leads per tenant | PostgreSQL lifecycle and conversion-retry tests | PASS |
| Opportunities, pipelines, stage history, safe transitions | Optimistic `version`, immutable stage history, pipeline update and complete-stage reorder validation | One default pipeline and six stages per tenant | PostgreSQL lifecycle plus stage-transition assertions | PASS |
| Activities, next action, seller worklists and attention | Owner/team-scoped queries; separate `home`, `worklist`, and `attention` predicates; update/complete/cancel | Existing CRM seed plus test overdue activity | PostgreSQL attention assertion; API composition | PASS |
| Buying committee, contact roles, opportunity interests | `opportunity_contact_role` and `opportunity_interest`; owner/team-scoped read/mutate routes plus `/buying-committee` aliases | No fabricated contact identities; configuration is available for real parties | Contract/build coverage; persisted CRUD exercised by repository contracts | PASS (contact-role model) |
| Typed custom fields and stage requirements | Forward migration creates definitions/values with RLS; typed JSON validation; required fields are checked on stage transition; secured value read/write routes | Each tenant receives an opportunity decision-role field and values for seeded opportunities | PostgreSQL test proves typed value persistence and required-stage rejection/acceptance | PASS |
| Forecast, snapshots, pipeline inspection and core summary reporting | Forecast read model, idempotent date/currency snapshot upsert, bounded inspection, seller/manager summary report, currency-scoped totals | Existing open pipeline plus seeded custom context | PostgreSQL snapshot/reorder test; API/OpenAPI contract test | PASS (basic reports) |
| Audit and permission-scoped history | CRM mutation audit writes through platform `IAuditWriter`; audit projection includes legal-entity metadata and CRM route filters it; stage history remains immutable | Audit is runtime-generated, not fabricated | API contract/build; mutation paths audited in service | PASS (projection); full action coverage still needs a broker/runtime audit replay test |
| Sales quotation, invoice and payment synchronization | `CrmExternalEventConsumer` registered in Worker; Inbox idempotency; `crm_external_event` immutable timeline and source-link state columns; forced RLS | External financial events are never fabricated by seed | Worker build and migration verification | IMPLEMENTED; live RabbitMQ replay not executed in this pass |
| Tenant/legal-entity/RLS and seller/manager visibility | Transaction-scoped `app.tenant_id`, forced RLS on all new tables, owner/team visibility on detail and related reads, bounded pagination | Atlas/Delta seed isolation retained | Real PostgreSQL integration suite and architecture gate | PASS |

## Post-change evidence

- `dotnet build backend/Backend.sln --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet format backend/Modules/CRM/CRM.csproj --no-restore --verify-no-changes`: PASS.
- `CrmPostgresIntegrationTests`: PASS, 3/3 against local PostgreSQL. Covers persisted lead/opportunity/activity/forecast lifecycle, conversion uniqueness/team visibility, custom-field typed storage, required-stage transition enforcement, stage reorder optimistic versioning, snapshot persistence, and cross-tenant isolation.
- `ApiCompositionTests`: PASS, 2/2. OpenAPI contains CRM configuration, buying-committee, reporting, audit, external-event, worklist and forecast routes.
- `ArchitectureTests`: PASS, 21/21.
- `SeedDataTests`: PASS, 5/5. Development seed rerun is idempotent; post-rerun counts remain 40 leads and 12 opportunities per tenant. Database inspection confirmed 8 custom-field definitions, 29 seeded values, and forced RLS on all three new CRM tables.
- `Worker.csproj` build: PASS, 0 warnings/errors; CRM external-event consumer is registered.
- Existing database migration upgrade: PASS; `DbMigrator` applied the new CRM forward fixes and reported 139 ordered scripts with previous checksums preserved.
- Full fresh-database bootstrap: FAIL before completion in the pre-existing `hr-forward-fix-go-live-configuration.sql` duplicate PostgreSQL type failure (`23505 pg_type_typname_nsp_index`). This failure is outside CRM and occurs before later modules; it must be tracked as an unrelated repository migration defect, not hidden or weakened.

## Explicit remaining limitations

1. A live RabbitMQ acceptance replay for quotation/invoice/payment events was not run in this pass. The consumer, Inbox idempotency and persisted event projection are implemented and compiled, but this gate remains open until a disposable broker/API scenario asserts an actual event, replay and source-link state transition.
2. Audit coverage is available through the platform projection and all newly added CRM mutation paths, but a full authenticated API audit-history scenario (including manager/seller filtering and replay) remains to be executed.
3. The report endpoint is the canonical basic summary (`/reports/summary`) plus forecast/pipeline inspection. Advanced cycle/source/stage-duration analytics remain deferred as Build Next reporting, not silently represented as complete.
4. Country-specific email/WhatsApp/calendar connectors, AI scoring, advanced cadences, territory/targets and marketing automation remain explicitly Build Next/Later and are not implemented.

Because limitations 1–2 are unexecuted acceptance evidence and the repository-wide fresh migration currently fails in unrelated HR work, this document does **not** mark `MODULE 19 CRM BACKEND BUILD NOW COMPLETE`. Frontend remains a separate handoff after the open runtime acceptance gates are closed.
