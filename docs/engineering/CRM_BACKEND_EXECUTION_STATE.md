# Module 19 CRM backend execution state

Canonical pack: `docs/modules/19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

## Delivered scope

The CRM module owns lead capture/qualification/assignment/conversion records, opportunities and stage history, activities and next actions, pipelines/stages, forecast read models, party relationship workspace, and the CRM-to-Sales quotation seam. Directory remains the owner of party identity and Sales remains the owner of quotations and revenue documents.

Implemented persistence is `business_crm` with tenant/legal-entity columns, indexes for inbox/pipeline/worklist queries, transaction-scoped `app.tenant_id`, RLS policies, and forced RLS finalization. Development seed is deterministic and rerunnable: two tenants receive pipelines, six stages, 40 leads, and 12 opportunities without replacing user-owned rows.

## API contract surface

- `GET/POST /api/v1/crm/legal-entities/{entityId}/leads`
- `GET/PUT /api/v1/crm/legal-entities/{entityId}/leads/{id}`
- `POST .../assign`, `.../qualify`, `.../disqualify`, `.../convert`
- `GET/POST /api/v1/crm/legal-entities/{entityId}/opportunities`
- `GET/PUT .../opportunities/{id}`, `POST .../stage`, `.../won`, `.../lost`, `.../quotation`
- `GET/POST /api/v1/crm/legal-entities/{entityId}/activities`, `POST .../{id}/complete`
- `GET/POST .../pipelines`, `POST .../pipelines/{pipelineId}/stages`
- `GET .../forecast`, `GET .../accounts/{partyId}/workspace`, `GET .../home`, `.../worklist`, `.../attention`

All scalable list contracts return `CrmPage<T>` with server-side page size, search/status/scope filters. Commands use optimistic versions and idempotency keys where an external retry can repeat the request.

## Evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| CRM schema + RLS bootstrap | PASS | `dotnet run --project backend/DbMigrator/DbMigrator.csproj` applied `crm-schema.sql` and `crm-forward-fix-runtime-permissions.sql`; finalization reapplied successfully. |
| Development seed | PASS | Seed output: each tenant 1 pipeline, 6 stages, 40 leads, 12 opportunities. |
| API/DbMigrator compile | PASS | `dotnet build backend/Api/Api.csproj --no-restore`; `dotnet build backend/DbMigrator/DbMigrator.csproj`; zero warnings/errors. |
| PostgreSQL business journey | PASS | `CrmPostgresIntegrationTests.LeadOpportunityActivityForecastLifecyclePersistsAndIsIdempotent`: persisted lead idempotent replay, duplicate candidate detection, qualification, opportunity creation/stage transition, activity completion, forecast delta, and cross-tenant isolation. 1 passed. |
| Migration checksum safety | PASS | Existing `crm-schema.sql` checksum preserved; runtime grants delivered as forward-fix. |

## Current Build Now evidence (latest local run)

The security/contract closure pass added owner and active-team visibility enforcement to detail reads and mutations, defaulted list scope to the authenticated seller, added active team membership management, idempotent lead import/export contracts, lead merge, opportunity assignment and forecast-category commands, activity update/cancel, distinct prioritized seller home/worklist/attention queries, and a unique lead→opportunity conversion constraint. Conversion retries now keep the first persisted opportunity and conversion record authoritative.

| Scenario | Result | Evidence |
| --- | --- | --- |
| PostgreSQL lead/opportunity/activity/forecast lifecycle | PASS | `CrmPostgresIntegrationTests.LeadOpportunityActivityForecastLifecyclePersistsAndIsIdempotent` (real PostgreSQL, 1 passed) |
| Team-member visibility + conversion retry uniqueness | PASS | `CrmPostgresIntegrationTests.TeamVisibilityAndConversionUniquenessAreEnforced` (real PostgreSQL, 1 passed) |
| Seller attention prioritization | PASS | Same integration test persists an overdue activity and asserts it is returned by the `attention` query |
| API/OpenAPI composition | PASS | `ApiCompositionTests` (2 passed) and OpenAPI assertions cover CRM import/export, team members and seller worklist routes |
| Build and formatting | PASS | `dotnet build backend/Backend.sln --no-restore` (0 warnings/errors); CRM `dotnet format --verify-no-changes` |
| Migration safety | PASS | `crm-forward-fix-conversion-uniqueness.sql` applied by DbMigrator; existing CRM baseline checksum unchanged |

## Remaining canonical Build Now gaps

The canonical pack still has capabilities not represented by the current backend contract and therefore are not marked complete: generic CRM audit projections, stage-specific custom-field definitions/validation, opportunity buying-committee and interest management APIs, pipeline edit/reorder APIs, forecast snapshot persistence endpoint, full core report/read-model contracts, and event consumers for quotation/invoice/payment synchronization. These are explicitly FAIL/deferred in this checkpoint; no `MODULE 19 ... COMPLETE` claim is made. Advanced cadences/automation, external email/WhatsApp/calendar connectors, AI scoring, territory/targets, marketing attribution and other Build Next/Later items remain out of scope. Frontend remains a separate mission.
