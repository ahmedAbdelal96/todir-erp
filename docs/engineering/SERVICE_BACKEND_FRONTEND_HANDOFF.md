# Module 24 Backend → Frontend Handoff

Backend status: `BACKEND COMPLETE`. Canonical pack: `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This handoff covers backend/database/seed/API readiness only. Frontend implementation, RTL/product UX acceptance, browser proof, and human Product Owner sign-off remain the frontend mission.

## API root and operation groups

Base path: `/api/v1/service`.

Requests and work: request list/create/triage/resolve/close/cancel; Work Order list/create/read and lifecycle actions; visits, assignments, dispatch resource requirements, crews, skills and territories.

Installed Base and execution: installed-base hierarchy/events, template versions, Work Order template snapshots, typed checklist answers, diagnosis, labor, downtime, reports with optional Platform `signedFileId`, immutable report acceptance, fixed-asset service history, and KPI including downtime, failure count, MTBF, MTTR and availability.

Maintenance and coverage: preventive plans, bounded due generation/conversion/state, maintenance rounds and subjects, typed meters with reset/rollover guard, SLA profiles/clocks/pause/resume/monitor, contracts/usage caps, warranties, entitlement resolution and supplier recovery.

Commercial and stock: charges, corrective charge links, project billing ownership, Invoicing draft request and allocation with optional invoice-line reference, and Inventory-backed part reserve/issue/consume/return/reverse.

Mobile/offline: `GET /mobile/my-day`, `POST /mobile/commands`; typed command states are idempotent and conflict-aware. Supported command names include visit transitions, Work Order start/technical completion, checklist answer, meter record and part consume.

## Contract rules the UI must preserve

- Send `rowVersion` on lifecycle mutations. A `409` requires refresh and an explicit conflict path.
- Customer Field Service requires both `customerPartyId` and `installedBaseId`; Internal Maintenance uses Fixed Asset or the approved Service subject boundary.
- Parts require legal entity, warehouse, location and UOM context for Inventory execution. Service counters change only after an owning Inventory result; reversal requires the original Inventory operation id.
- `BillingOwner=Projects` requires `projectId`; Project-owned work cannot request a Service invoice. Service invoice requests are accepted only after source identity, party, legal entity, direction and currency validation.
- Entitlement is line-level. Render `covered`, `coveredAmount`, `customerAmount`, `coverageSource` and `explanation`; do not infer coverage from a zero amount.
- Accepted reports are immutable. A signed file is a Platform reference, not a Service-owned file blob.
- Offline replay is idempotent by `clientCommandId`; `Pending`, `Processed`, `Rejected`, `Retryable` and `Conflict` are business states, not proof of success by themselves.
- Backend authorization and tenant/legal-entity isolation are authoritative. Do not use frontend filters or hidden identifiers as a security boundary.

## Canonical development seed

Atlas Trading and Delta Supplies contain installed base, requests, customer/internal Work Orders, visits/assignments, real Catalog and Inventory part references, labor/cost/downtime, active contract/usage, Fixed Asset maintenance plan/due, meter, checklist, diagnosis and accepted report data. The seed is deterministic and rerunnable.

## Acceptance evidence

Fresh disposable acceptance after the final relevant source change: Service integration 4/4, Service unit 5/5, Architecture 21/21, SeedData 5/5, 215 migration scripts applied/verified, forced-RLS and tenant-isolation checks passed, and cross-module Inventory/Invoicing/Projects/Fixed Assets ownership checks passed.

Deferred scope remains exactly the canonical pack's advanced route optimization, continuous GPS, IoT predictive maintenance, advanced spare-parts optimization, depot-repair logistics, full supplier claims, advanced EHS/fleet, autonomous AI, complex global revenue recognition, customer portal and remote AR/IoT.
