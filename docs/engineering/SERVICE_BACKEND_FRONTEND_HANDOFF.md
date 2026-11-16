# Module 24 Backend → Frontend Handoff

This is a contract handoff, not a `MODULE COMPLETE` claim. Backend status is `NOT VERIFIED` until the SV00–SV51 real-stack acceptance register is fresh after the corrective changes.

## API root and operation groups

Base path: `/api/v1/service`.

Requests: `GET /requests`, `POST /requests`, and `POST /requests/{id}/triage|resolve|close|cancel`.

Work: `GET|POST /work-orders`, `GET /work-orders/{id}`, lifecycle actions `ready|schedule|start|waiting|technical-complete|accept|close|cancel|reopen`, visits under work orders, `GET /visits`, visit actions `dispatch|travel|arrive|start|complete|no-access|cancel`, and assignments.

Installed base and execution records: installed-base list/create, templates list/create, checklist answers, diagnoses, component events, labor, downtime start/end, reports save/accept, and KPI.

Maintenance: maintenance-plan list/create, `POST /maintenance-due/generate` with a bounded date horizon, due list, manual due creation, due state (`Due|Postponed|Skipped`) with `expectedVersion`, and conversion to a work order.

Meters/SLA: meter readings; SLA profiles with business weekdays/time window/time zone; clocks; pause/resume; bounded breach monitor.

Commercial: contracts/activation/usage, warranties, `POST /entitlements/resolve` with line-level `chargeCategory`, `amount`, `quantity`, and `description`; charges; invoice request; invoice allocation; parts reserve/issue/consume/return.

Mobile/offline: `GET /mobile/my-day`, `POST /mobile/commands`. Supported typed command names are `visit.arrive`, `visit.start`, `visit.complete`, `visit.no-access`, `work.start`, `work.technical-complete`, `checklist.answer`, `meter.record`, and `part.consume`.

## Contract rules the UI must preserve

- Send `rowVersion` on lifecycle mutations. A `409` with `service.concurrent_change` or a related code requires refresh and a user-visible conflict path.
- `CustomerFieldService` requires both `customerPartyId` and `installedBaseId`; internal maintenance requires a subject as defined by the request contract.
- Parts require `legalEntityId`, `warehouseId`, `locationId`, and `unitOfMeasureId` for Inventory execution. Service counters change only after an owning Inventory result. Show `Pending`, `Retryable`, and `Processed` integration states distinctly.
- `BillingOwner=Projects` requires `projectId`; Project-owned charges are sent to Projects cost facts and cannot request a Service invoice. Service-owned invoice requests return a real Invoicing draft id only after source identity, legal entity, party, direction and currency are verified.
- Entitlement responses are line-level. Never infer coverage from a zero amount; use `covered`, `coveredAmount`, `customerAmount`, `coverageSource`, and `explanation`.
- Offline replay is idempotent by `clientCommandId`. `Pending`, `Processed`, `Rejected`, `Retryable`, and `Conflict` are business states, not HTTP success substitutes. Never display `{"accepted":true}` as proof of execution.
- Tenant and legal-entity authorization is backend-enforced. Do not use hidden identifiers or a frontend-only legal-entity filter as a security boundary.
- Backend status values are API language. Use the governed Arabic ERP glossary, RTL layouts, and the separate frontend mission rules for user-facing copy.

## Seed scenarios available for frontend development

For each of Atlas Trading and Delta Supplies: one installed base, one customer request, one customer work order, one internal work order, one completed visit/assignment, one consumed part tied to a real catalog variant and Inventory warehouse/location/UOM, labor, cost, downtime, active contract/usage, fixed-asset maintenance plan/due, meter reading, checklist, diagnosis and accepted report. Seed is deterministic and rerunnable.

## Acceptance still required before frontend handoff is final

Wait for fresh backend evidence covering real Inventory movement history, real Invoicing draft/allocation, Projects cost-fact ownership, offline replay/conflict, preventive/meter/SLA workflows, tenant/legal-entity authorization, and the SV00–SV51 release gate. No frontend code is included in this backend mission.
