# Module 26 Manufacturing / MRP Backend-to-Frontend Handoff

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This is a backend handoff draft, not `FRONTEND COMPLETE` or `MODULE COMPLETE`. The frontend mission must consume the public API and shared capabilities; it must not write Manufacturing or Inventory tables.

## Available API surface

- `/api/v1/manufacturing/plants`
- `/api/v1/manufacturing/product-profiles`
- `/api/v1/manufacturing/boms`, `/boms/{id}/versions`, `/bom-versions/{id}/approve|activate|supersede`
- `/api/v1/manufacturing/formulas`, `/formulas/{id}/versions`
- `/api/v1/manufacturing/routings`, `/routings/{id}/versions`
- `/api/v1/manufacturing/work-centers`, `/work-centers/{id}/calendar`
- `/api/v1/manufacturing/definitions`, `/definitions/resolve`
- `/api/v1/manufacturing/substitution-groups`
- `/api/v1/manufacturing/production-orders` and lifecycle commands
- `/api/v1/manufacturing/production-orders/{id}/materials` plus reserve/issue/consume/return aliases
- `/api/v1/manufacturing/production-orders/{id}/outputs|scrap|cost|cost-facts`
- `/api/v1/manufacturing/planning-runs`, demand/run/supplies, and planned-production firming
- `/api/v1/manufacturing/production-orders/{id}/operations`, operation action commands, close-readiness, plant capacity, and genealogy forward/reverse reads

The planning-run execution endpoint refreshes accepted Sales demand and eligible Purchasing supply through owner capabilities before calculating Manufacturing suggestions. A missing capability is treated as an empty optional source; the current development fixture still needs non-empty source-line acceptance coverage.

All endpoints require authentication, tenant context, the relevant `manufacturing.*` permission, and the API rate limit. Backend errors expose a stable `code`; frontend copy must map it to Arabic-market terminology and never display SQL/ORM details.

## Domain safety invariants for the frontend

- Display required, reserved, issued, consumed, returned, and scrapped as separate quantities.
- A returned quantity is not a negative consumption and output receipt is not order completion.
- A released order renders its definition snapshot; never offer an edit that silently changes the released BOM/formula/routing.
- Show provisional output value and Inventory receipt status separately from final manufacturing cost.
- Do not present MRP suggestions as authorized production or purchase orders until explicitly firmed.
- Show forecast consumption and accepted sales demand in the MRP explanation; never add them blindly.
- Use explicit retry/idempotency keys for shop-floor and material/output commands.

## Development seed scenarios available for UI evaluation

- Atlas and Delta each have a main plant, active finished-goods BOM and approved production definition.
- `MO-SEED-0001` contains 100 ordered, 60 completed, component material with 100 issued / 60 consumed / 40 returned, a pending provisional 60-unit output, planned/actual cost facts, and an explainable variance.
- MRP seed contains 1,000 forecast units with 600 consumed by accepted demand and a 400-unit residual planned production suggestion.

## Not ready for frontend acceptance yet

The backend checkpoint remains `NOT COMPLETE` until the remaining MF27–MF35 and MF45–MF47 integrations, non-empty source fixtures, linked genealogy/output recovery, and authenticated isolated acceptance tests are delivered. Frontend work may use the stable master-data, order/material/cost/planning, operation, close-readiness, and capacity endpoints for contract exploration, but must not treat the module as production-ready. The final backend runtime evidence is recorded in `MANUFACTURING_BACKEND_COVERAGE_MATRIX.md`.
