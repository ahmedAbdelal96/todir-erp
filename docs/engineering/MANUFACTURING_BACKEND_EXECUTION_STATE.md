# Module 26 Manufacturing / MRP Backend Execution State

## Delivery status

`BACKEND COMPLETE`

## Canonical scope

The completed scope is the backend/database/integration/development-seed delivery defined by `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`, MF00–MF47. Frontend implementation and Product/UX acceptance are separate missions.

## Completed

- Manufacturing owns its `business_manufacturing` schema, forced tenant RLS, audit/idempotency records, forward-only migrations, and deterministic Atlas/Delta seed scenarios.
- Product profiles, plants, BOM revisions, formulas, co-/by-products, routings, work centers, calendars, resources, substitutions, definitions and immutable production snapshots are implemented.
- Production orders, material conservation, Inventory reservation/issue/consume/return/output boundaries, partial output, scrap, yield, quality gates, operations, labor, subcontract commitments, genealogy, close readiness, planned/actual cost, variance, WIP and cost allocation are implemented.
- MRP consumes Sales, CRM Won opportunity interests, Purchasing supply and Inventory availability through owner capabilities. Forecast consumption, UOM normalization, phantom multi-level explosion, dependent demand, pegging, planned supply, firming and MPS publication are implemented.
- Capacity consumes maintenance downtime through the maintenance capability; project-linked labor and subcontract facts are recorded in Projects exactly once through typed source integration.
- Manufacturing events use the existing transactional Outbox/RabbitMQ/Worker/Inbox path with Manufacturing idempotency and audit protection.
- Final local gates passed after the last source change: build, Release build, format verification, Manufacturing/Sales/Purchasing/Service/Projects tests, CRM integration, Projects cross-module RabbitMQ acceptance, Security, Architecture, SeedData, migration verification and `git diff --check`.

## Fresh runtime evidence

- Authenticated production order lifecycle reached `Released` and `InProgress`.
- Authenticated MRP refresh persisted `CrmWonOpportunity=1`, `SalesOrder=1` and dependent demand rows.
- Capacity returned maintenance downtime and remaining capacity for the seeded work center.
- MPS create/publish/list returned HTTP 201/200/200.
- A required quality gate with unavailable capability persisted `Blocked` and prevented release.
- Replayed labor and subcontract requests resulted in one Projects cost fact per idempotency key, both linked to the project.
- Two production lifecycle events were `Published` by Outbox and each was processed once by `manufacturing-integration` in Inbox and audit.

## Deferred / separate gates

- PJ38 heavyweight performance certification is deferred by the Product Owner.
- Frontend implementation, browser UX evidence and human Product/UX acceptance are not part of this backend delivery and are handed off through `MANUFACTURING_BACKEND_FRONTEND_HANDOFF.md`.

## Last meaningful change

The final implementation pass added CRM Won opportunity demand ownership, recursive phantom planning source preservation, MPS publication/read routes, formula/resource seed coverage, project-linked labor/subcontract cost ingress, maintenance downtime capability wiring, and the final operational/migration extensions. All affected runtime and test evidence was recaptured afterward.

## Delivery checkpoint

Backend is ready for frontend handoff. The next mission may build the frontend against the documented routes and stable error/idempotency contracts; it must not write Manufacturing or Inventory tables directly.
