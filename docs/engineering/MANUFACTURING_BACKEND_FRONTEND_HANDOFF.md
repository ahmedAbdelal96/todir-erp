# Module 26 Manufacturing / MRP Backend-to-Frontend Handoff

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

Status: `BACKEND COMPLETE — DEVELOPMENT SEED READY — FRONTEND HANDOFF`.

The frontend consumes the public API and shared capabilities. It must not write Manufacturing, Inventory, CRM, Purchasing, Maintenance or Projects tables directly.

## Available API surface

- `/api/v1/manufacturing/plants`, `/product-profiles`, BOM/version lifecycle, formulas/version, routings/version and work-center/calendar routes.
- `/api/v1/manufacturing/definitions`, `/definitions/resolve`, substitution groups and production-definition routes.
- `/api/v1/manufacturing/production-orders` and lifecycle commands; materials reserve/issue/consume/return; outputs, scrap, cost facts and output-cost allocations.
- `/api/v1/manufacturing/production-orders/{id}/operations`, operation actions, operation resources, labor, quality gates, subcontract commitments, close-readiness and genealogy.
- `/api/v1/manufacturing/planning-runs`, demand/run/supplies, planned-production firming, `/mps-plans` create/list/publish and plant capacity.

Every endpoint requires authentication, tenant context, the relevant `manufacturing.*` permission and API rate limiting. Stable backend error `code` values must be mapped to Arabic-market copy; SQL, ORM details and stack traces must never be displayed.

## Domain safety invariants

- Keep required, reserved, issued, consumed, returned and scrapped quantities separate.
- A returned quantity is not negative consumption; output receipt is not order completion.
- A released order renders its immutable definition snapshot. Never silently edit its BOM, formula or routing.
- Show Inventory receipt status and canonical valuation separately from Manufacturing cost. Client provisional values are not authoritative.
- MRP suggestions are not production or purchase orders until explicitly firmed.
- Explain forecast consumption, accepted Sales demand and CRM Won demand separately; never double-count them.
- Use the same idempotency key when retrying material, output, labor, subcontract and shop-floor commands after an unknown result.
- Quality `Blocked`/`Pending` states must remain visibly non-executable until the owning Quality capability returns a valid decision.

## Seed scenarios available for UI evaluation

- Atlas and Delta each have a main plant, active finished-goods BOM, approved definition, work-center calendar/resource and service downtime.
- `MO-SEED-0001` contains partial production, distinct issued/consumed/returned quantities, provisional output, planned/actual cost and variance evidence.
- MRP includes forecast, confirmed Sales, approved Purchasing supply, CRM Won opportunity demand, phantom/dependent demand and pegging evidence.
- A project-linked production order demonstrates replay-safe labor and subcontract cost facts in Projects.
- Formula seed includes ingredient, primary/residual and by-product output lines.

## Integration expectations

Manufacturing refreshes owner sources through capabilities: Sales/CRM own demand, Purchasing owns supply/commitment truth, Inventory owns movement/valuation, Service owns downtime, Quality owns inspection decisions, Projects owns project cost projections, and Ledger owns semantic accounting postings. The frontend should present these as linked explanations, not duplicate editable facts.

## Acceptance handoff

Backend local evidence is recorded in `MANUFACTURING_BACKEND_COVERAGE_MATRIX.md` and `MANUFACTURING_BACKEND_EXECUTION_STATE.md`. PJ38 heavyweight performance certification is deferred by the Product Owner. Frontend work now proceeds as a separate mission with RTL, Arabic ERP terminology, responsive states, permission-aware actions and real seeded API verification.
