# Module 24 Backend → Frontend Handoff

Canonical pack: `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Backend scope is complete for the documented Build Now surface. This document is the frontend contract; it does not claim frontend, UX, browser, or human Product Owner acceptance.

## Shared HTTP contract

Base URL: `/api/v1/service`. Every route requires the authenticated session and API rate limit. Mutating requests require the CSRF token header used by platform auth. JSON uses serialized DTO property names (`legalEntityId`, `rowVersion`, `hasNext`, `totalCount`). UUIDs are strings and dates/times are ISO-8601.

Every paged response is `PagedResult<T>`: `{ "items": [], "page": 1, "pageSize": 50, "hasNext": false, "totalCount": 0 }`. Page numbers start at 1 and `pageSize` is bounded by the backend. List filters are server-side. A legal-entity-scoped membership must send `legalEntityId` on tenant-wide list contracts; frontend filtering is never a security boundary.

Standard errors are Problem Details with `extensions.code`. Anonymous requests are `401`; missing permission, wrong legal entity, wrong tenant, or revoked technician assignment is `403`; invalid business input or an unavailable/forged Platform File is `400`; missing resource is `404`; optimistic-version, idempotency, and integration conflicts are `409`. Do not display SQL, table names, stack traces, or raw exception text.

## Exact route contract

| Method and path | Request / response | List query and scope | Permission and legal-entity rule |
|---|---|---|---|
| `GET /requests` | `PagedResult<ServiceRequestView>` | `legalEntityId`, `state`, `search`, `page`, `pageSize` | `service.request.read`; legal entity |
| `POST /requests` | `CreateServiceRequest` → `ServiceRequestView` | — | `service.request.create`; body legal entity |
| `POST /requests/{requestId}/triage\|resolve\|close\|cancel` | empty → `ServiceRequestView` | — | triage `service.request.triage`; other actions `service.request.resolve`; resource entity |
| `GET /work-orders` | `PagedResult<WorkOrderView>` | `legalEntityId`, `state`, `contextName`, `page`, `pageSize` | `service.work.read`; legal entity |
| `POST /work-orders` | `CreateWorkOrderRequest` → `WorkOrderView` | — | `service.work.create`; body legal entity |
| `GET /work-orders/{workOrderId}` | `WorkOrderView` | — | `service.work.read`; resource entity |
| `POST /work-orders/{workOrderId}/ready\|schedule\|start\|waiting\|technical-complete\|accept\|close\|cancel\|reopen` | `TransitionRequest` → `WorkOrderView` | — | plan/execute/close/reopen permission by action; resource entity; send `rowVersion` |
| `GET /work-orders/{workOrderId}/visits` | `PagedResult<VisitView>` | `state`, `page`, `pageSize` | `service.work.read`; parent entity |
| `POST /work-orders/{workOrderId}/visits` | `CreateVisitRequest` → `VisitView` | — | `service.work.schedule`; parent entity |
| `GET /visits` | `PagedResult<VisitView>` | `workOrderId`, `state`, `page`, `pageSize` | `service.work.read`; supplied work-order entity |
| `POST /visits/{visitId}/dispatch\|travel\|arrive\|start\|complete\|no-access\|cancel` | `TransitionRequest` → `VisitView` | — | schedule or execute by action; visit entity; send `rowVersion` |
| `POST /visits/{visitId}/assignments` | `CreateAssignmentRequest` → `AssignmentView` | — | `service.dispatch.manage`; visit entity |
| `GET/POST /work-orders/{workOrderId}/resource-requirements` | GET `ResourceRequirementView[]`; POST `CreateResourceRequirementRequest` → item | no paging | dispatch read/manage; parent entity |
| `GET /crews` | `CrewView[]` | required `legalEntityId` | `service.dispatch.read`; legal entity |
| `POST /crews` | `CreateCrewRequest` → `CrewView` | — | `service.dispatch.manage`; body entity |
| `GET /skills`, `GET /territories` | `SkillView[]` / `TerritoryView[]` | no paging/filter | `service.dispatch.read`; tenant reference data |
| `POST /skills`, `POST /territories` | create DTO → view | — | `service.dispatch.manage`; tenant reference data |
| `GET /entities/{entityId}/external-references` | `ExternalReferenceView[]` | optional `entityType` (`WorkOrder`, `ServiceRequest`, `Visit`, `InstalledBase`) | `service.work.read`; referenced entity |
| `POST /external-references` | `CreateExternalReferenceRequest` → `ExternalReferenceView` | — | `service.work.execute`; referenced entity |
| `GET /installed-base` | `PagedResult<InstalledBaseView>` | `legalEntityId`, `customerPartyId`, `search`, `page`, `pageSize` | `service.installed_base.read`; legal entity |
| `POST /installed-base` | `CreateInstalledBaseRequest` → `InstalledBaseView` | — | `service.installed_base.manage`; body entity |
| `GET /templates` | `PagedResult<TemplateView>` | `legalEntityId`, `page`, `pageSize` | `service.template.read`; legal entity |
| `POST /templates` | `CreateTemplateRequest` → `TemplateView` | — | `service.template.manage`; body entity |
| `POST /work-orders/{workOrderId}/checklist-answers` | `ChecklistAnswerRequest` → `ChecklistAnswerView` | — | `service.work.execute`; parent entity |
| `POST /work-orders/{workOrderId}/diagnoses` | `DiagnosisRequest` → `DiagnosisView` | — | `service.work.execute`; parent entity |
| `POST /installed-base/{itemId}/component-events` | `ComponentEventRequest` → `ComponentEventView` | — | `service.installed_base.manage`; item entity |
| `GET /maintenance-plans` | `PagedResult<MaintenancePlanView>` | `legalEntityId`, `page`, `pageSize` | `service.maintenance_plan.read`; legal entity |
| `POST /maintenance-plans` | `CreateMaintenancePlanRequest` → `MaintenancePlanView` | — | `service.maintenance_plan.manage`; body entity |
| `POST /maintenance-due/generate` | `GenerateDueRequest` → `DueGenerationView` | — | `service.maintenance_plan.manage`; tenant scheduled operation |
| `POST /maintenance-plans/{planId}/due` | query `dueDate` → `MaintenanceDueView` | — | `service.maintenance_plan.manage`; plan entity |
| `GET /maintenance-due` | `PagedResult<MaintenanceDueView>` | `legalEntityId`, `state`, `from`, `to`, `page`, `pageSize` | `service.maintenance_plan.read`; plan entity |
| `POST /maintenance-due/{dueId}/convert` | `ConvertDueRequest` → `MaintenanceConversionView` | — | `service.maintenance_plan.manage`; due/plan entity; body entity must agree |
| `POST /maintenance-due/{dueId}/state` | `MaintenanceDueUpdateRequest` → `MaintenanceDueView` | — | `service.maintenance_plan.manage`; due/plan entity; expected version |
| `POST /maintenance-rounds` | `CreateMaintenanceRoundRequest` → `MaintenanceRoundView` | — | `service.maintenance_plan.manage`; body entity |
| `POST /maintenance-rounds/{roundId}/subjects` | `AddRoundSubjectRequest` → `RoundSubjectView` | — | `service.maintenance_plan.manage`; round entity |
| `POST /maintenance-rounds/{roundId}/plan\|start\|complete\|cancel` | `TransitionRequest` → `MaintenanceRoundView` | — | `service.maintenance_plan.manage`; round entity; `rowVersion` |
| `POST /maintenance-round-subjects/{subjectId}/record` | `RecordRoundSubjectRequest` → `RoundSubjectView` | — | `service.maintenance_plan.manage`; round entity |
| `POST /subjects/{subjectId}/meter-readings` | `RecordMeterReadingRequest` → `MeterReadingView` | — | `service.meter.record`; body entity and subject agree |
| `POST /contracts` | `CreateContractRequest` → `ServiceContractView` | — | `service.contract.manage`; body entity |
| `POST /contracts/{contractId}/activate` | empty → `ServiceContractView` | — | `service.contract.manage`; contract entity |
| `POST /contracts/{contractId}/usage` | `ContractUsageRequest` → `ServiceContractView` | — | `service.contract.manage`; contract entity |
| `POST /warranties` | `CreateWarrantyRequest` → `WarrantyView` | — | `service.warranty.manage`; body entity |
| `POST /supplier-recoveries` | `CreateSupplierRecoveryRequest` → `SupplierRecoveryView` | — | `service.warranty.manage`; work-order entity |
| `POST /supplier-recoveries/{recoveryId}/{state}` | empty → `SupplierRecoveryView` | state `Requested`, `Recovered`, `Rejected`, or `Cancelled` | `service.warranty.manage`; recovery entity |
| `POST /sla/profiles` | `CreateSlaProfileRequest` → `SlaProfileView` | — | `service.sla.manage`; body entity |
| `POST /sla/clocks` | `StartSlaClockRequest` → `SlaClockView` | — | `service.sla.manage`; work-order entity |
| `POST /sla/clocks/{workOrderId}/{clockType}/pause\|resume` | `SlaClockActionRequest` → `SlaClockView` | — | `service.sla.manage`; work-order entity |
| `POST /sla/monitor` | → `SlaMonitorView` | — | `service.sla.manage`; tenant scheduled operation |
| `POST /entitlements/resolve` | `EntitlementRequest` → `EntitlementView` | — | `service.contract.read`; body entity |
| `GET /work-orders/{workOrderId}/charges` | `PagedResult<ServiceChargeView>` | `page`, `pageSize` | `service.billing.read`; work-order entity; cost permission |
| `POST /work-orders/{workOrderId}/charges` | `CreateChargeRequest` → `ServiceChargeView` | — | `service.billing.manage`; work-order entity |
| `POST /charges/{chargeId}/request-invoice` | → `ServiceChargeView` | — | `service.billing.request_invoice`; charge entity |
| `POST /charges/{chargeId}/allocate-invoice` | `InvoiceAllocationRequest` → `ServiceChargeView` | — | `service.billing.request_invoice`; charge entity |
| `POST /work-orders/{workOrderId}/parts` | `CreatePartRequirementRequest` → `PartRequirementView` | — | `service.parts.request`; work-order entity; body cannot override entity |
| `POST /parts/{partId}/reserve\|issue\|consume\|return\|reverse` | `PartActionRequest` → `PartRequirementView` | — | request/consume permission by action; part entity |
| `POST /labor` | `RecordLaborRequest` → `LaborView` | — | `service.work.execute`; visit entity |
| `POST /subjects/{subjectId}/downtime` | `DowntimeRequest` → `DowntimeView` | — | `service.work.execute`; work-order or installed-base entity |
| `POST /downtime/{downtimeId}/end` | query `endedAt` → `DowntimeView` | — | `service.work.execute`; downtime entity |
| `POST /work-orders/{workOrderId}/report` | `CreateReportRequest` → `ServiceReportView` | — | `service.documents.manage`; work-order entity |
| `POST /reports/{reportId}/accept` | query `version` → `ServiceReportView` | — | `service.documents.manage`; report entity |
| `GET /mobile/my-day` | `MobileDayView` | `date`; employeeId is not a security selector | `service.work.read`; authenticated user assignment scope |
| `POST /mobile/commands` | `OfflineCommandRequest` → `202 OfflineCommandView` | — | `service.work.execute`; active assignment required |
| `GET /dispatch/unscheduled` | `PagedResult<WorkOrderView>` | `legalEntityId`, `page`, `pageSize`; fixed state `Ready` | `service.dispatch.read`; legal entity |
| `GET /fixed-assets/{fixedAssetId}/service-history` | `PagedResult<FixedAssetServiceHistoryView>` | `page`, `pageSize` | `service.work.read`; history work-order entity |
| `GET /reports/kpi` | `ServiceKpiView` | no query | `service.export`; tenant-wide cost/report permission |

## DTO, lifecycle, and integration rules

DTO definitions are in `backend/Modules/Service/DTOs/ServiceContracts.cs`. Preserve `rowVersion` on Work Orders, Visits, Maintenance Due, and Rounds. Lifecycle actions are server transitions; never synthesize a state after a failed request.

Requests are `New → Triaged → Resolved → Closed` with cancellation before close. Work Orders are `Draft → Ready → Scheduled → InProgress/Waiting → TechnicalCompleted → Accepted → Closed`, with cancellation and controlled reopen. Visits support dispatch/travel/arrival/start/complete, no-access, and cancel. Accepted reports are immutable and later saves return `service.report_immutable`.

Inventory states are `Pending`, `Processed`, `Retryable`, `Failed`, and `Reversed`; Service quantities change only after Inventory confirms. Invoice integration states are `Pending`, `Requested`, `Allocated`, `Failed`, and `Cancelled`; Project-owned work cannot request a Service invoice. Entitlement is line-level: render `covered`, `coveredAmount`, `customerAmount`, `coverageSource`, and `explanation`.

## Platform Files and offline contract

`signedFileId` is an optional Platform Files reference for draft reports. If supplied, the backend verifies a `Ready` file owned by `service / work_order / {workOrderId}`, in the same tenant and Service legal-entity resource scope, with allowed report classification `internal` or `confidential`. Missing, forged, wrong-owner, unavailable, cross-tenant, or disallowed-classification identifiers return `service.report_file_reference_invalid`. Acceptance re-verifies the reference and records the immutable historical version. The UI never uploads a Service-owned blob.

`OfflineCommandRequest` includes `clientCommandId`, `commandType`, optional `workOrderId`/`visitId`, optional `expectedVersion`, and typed `payloadJson`. Supported commands: `visit.arrive`, `visit.start`, `visit.complete`, `visit.no-access`, `work.start`, `work.technical-complete`, `checklist.answer`, `meter.record`, and `part.consume`. Replays return the original result. After assignment revocation the API returns `403` and accepts no new command. Stale versions become `Conflict` with `service.concurrent_change`.

## Seeded journeys and evidence

Administrator, dispatcher/manager, technician, and finance/billing journeys are seeded for Atlas Trading and Delta Supplies with customer/internal Work Orders, Installed Base hierarchy, visits/assignments, templates/checklists/diagnosis/reports, preventive/meter/SLA/contract data, real Catalog and Inventory references, costs/downtime/KPI facts, Project and Invoicing ownership, and fixed-asset history. A restricted Atlas membership is limited to one legal entity; cost routes and secondary-entity resources are forbidden, and Delta resources remain isolated.

Fresh backend evidence after this security/contract correction must include solution build, Service unit/integration regressions, authenticated HTTP scope tests, real Platform File fixtures, worker cycle behavior, seed verification, architecture checks, forced-RLS/tenant isolation, and formatting/diff checks. Heavyweight 100k/1M-row performance certification was not executed and is not claimed as PASS; it remains a separate Performance Hardening gate. Normal bounded pagination, ownership joins, and indexes are the applicable Module 24 gate.

Frontend/UX remains responsible for Arabic-first RTL journeys, responsive/loading/empty/error states, API error mapping, and human Product Owner acceptance. Deferred scope remains advanced route optimization, continuous GPS, IoT predictive maintenance, advanced spare-parts optimization, full depot-repair logistics, full supplier warranty claims, advanced EHS/fleet, autonomous AI dispatch/warranty, complex global contract revenue recognition, customer portal, and remote AR/IoT diagnostics.
