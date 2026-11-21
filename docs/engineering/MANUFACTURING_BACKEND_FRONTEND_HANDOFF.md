# Module 26 Manufacturing / MRP Backend-to-Frontend Handoff

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Status: `BACKEND COMPLETE + DEVELOPMENT SEED READY — FRONTEND HANDOFF`.

The P0 acceptance correction is complete locally: MRP stock netting is authoritative across the recursive explosion, Quality required state is server-controlled and fail-closed, close requires reconciled WIP/Ledger facts, and genealogy accepts only Inventory-owned lot identities. The frontend must treat these states as authoritative and must not offer a client-side bypass.

The frontend consumes the public API and shared capabilities. It must not write Manufacturing, Inventory, CRM, Purchasing, Maintenance, Fixed Assets or Projects tables directly.

## Available API surface

- `/api/v1/manufacturing/plants`, product profiles, BOM/version lifecycle, formulas/version, routings/version and work-center/calendar routes.
- `/api/v1/manufacturing/definitions`, definition resolution, substitution groups and production-definition routes.
- `/api/v1/manufacturing/production-orders` and lifecycle commands; material reserve/issue/consume/return; outputs, scrap, cost facts and output-cost allocations.
- `/api/v1/manufacturing/production-orders/{id}/operations`, operation actions/resources, labor, quality gates, subcontract commitments, close-readiness and genealogy.
- `/api/v1/manufacturing/planning-runs`, demand/run/supplies, planned-production firming, `/mps-plans` create/list/publish and plant capacity.

Every endpoint requires authentication, tenant context, the relevant `manufacturing.*` permission and rate limiting. Map stable backend `code` values to Arabic-market copy; never expose SQL, ORM details or stack traces.

## Domain safety invariants

- Keep required, reserved, issued, consumed, returned and scrapped quantities separate.
- A returned quantity is not negative consumption; output receipt is not order completion.
- A released order renders an immutable definition snapshot. Never silently edit its BOM, formula or routing.
- Show Inventory receipt status and canonical valuation separately from Manufacturing cost. Client provisional values are not authoritative.
- MRP suggestions are not production or purchase orders until explicitly firmed.
- Explain forecast, accepted Sales demand and CRM Won demand separately; never double-count owner sources.
- Reuse the same idempotency key after an unknown material, output, labor, subcontract or shop-floor result.
- Quality `Blocked`/`Pending` states are visibly non-executable until the Quality owner returns a verified decision. The client cannot mark a decision as provider-verified.
- MRP displays allocated stock and uncovered requirement separately; forecast and accepted Sales demand must not be added twice, and a planning rerun must not duplicate dependent demand or pegging.
- Close actions must surface the exact WIP/Ledger reconciliation blocker; a client cannot force close with positive unexplained WIP, unposted facts, missing journal linkage or an unbalanced journal.
- Genealogy lot selectors must use Inventory-owned confirmed lots for the exact product; never accept a user-entered lot ID as proof of identity.
- Labor rate is the explicit Manufacturing rate until an approved HR payroll-rate capability is available.

## Seed scenarios available for UI evaluation

- Atlas and Delta each have a main plant, active finished-goods BOM, approved definition, work-center calendar/resource, service downtime seam and project data.
- `MO-SEED-0001` contains distinct issued/consumed/returned quantities, provisional output, planned/actual cost and variance evidence.
- MRP includes forecast, confirmed Sales, approved Purchasing supply, CRM Won opportunity demand, phantom/dependent demand and pegging evidence.
- A project-linked production order demonstrates replay-safe labor and subcontract cost facts.
- Formula seed includes ingredient, primary/residual and by-product output lines.

## Integration expectations

Manufacturing refreshes owner sources through capabilities: Sales/CRM own demand, Purchasing owns supply, Inventory owns movement/valuation, Service owns downtime, Quality owns inspection decisions, Projects owns project cost projections and Ledger owns semantic accounting postings. Present these as linked explanations, not duplicate editable facts.

## Acceptance handoff

Local evidence is recorded in `MANUFACTURING_BACKEND_COVERAGE_MATRIX.md` and `MANUFACTURING_BACKEND_EXECUTION_STATE.md`. PJ38 heavyweight performance certification is deferred by the Product Owner. Frontend work proceeds as a separate mission with RTL, Arabic ERP terminology, responsive states, permission-aware actions and real seeded API verification.
