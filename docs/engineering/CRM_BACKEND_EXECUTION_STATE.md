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

## Explicit limitations / deferred pack scope

The Build Next/Build Later items remain intentionally outside this backend handoff: advanced cadence/automation, email/WhatsApp/calendar connectors, AI scoring, territory/target suite, marketing attribution, and external lead-provider connectors. CSV import/export and full manager/team CRUD require a subsequent bounded contract pass; no fake implementation is exposed. Frontend remains a separate mission.
