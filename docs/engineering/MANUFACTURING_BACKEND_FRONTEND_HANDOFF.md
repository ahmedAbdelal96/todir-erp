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

The planning-run execution endpoint refreshes accepted Sales demand and eligible Purchasing supply through owner capabilities before calculating Manufacturing suggestions. Missing owner capabilities fail closed. The canonical development fixture provides one confirmed 600-unit PACK Sales line and one approved 100-unit EA Purchasing line per tenant inside the horizon; the run persists source-line references, due dates, and a `salesDemandCount=1` / `purchaseSupplyCount=1` watermark. Sales entered units are converted through Catalog before MRP compares them with Inventory base units.

Execution commands now validate the production order and output identity/quantity before invoking Inventory. Inventory-backed material/output facts require a tenant/legal-entity-scoped confirmed operation snapshot; a retry can recover a committed Inventory operation by the Manufacturing source key. New material/output keys are bound to a request hash and reuse with a different payload is rejected. The frontend must treat an unknown Inventory outcome as recoverable and retry with the same idempotency key; it must not create a new key automatically.

All endpoints require authentication, tenant context, the relevant `manufacturing.*` permission, and the API rate limit. Backend errors expose a stable `code`; frontend copy must map it to Arabic-market terminology and never display SQL/ORM details.

## Domain safety invariants for the frontend

- Display required, reserved, issued, consumed, returned, and scrapped as separate quantities.
- A returned quantity is not a negative consumption and output receipt is not order completion.
- A released order renders its definition snapshot; never offer an edit that silently changes the released BOM/formula/routing.
- Show the confirmed Inventory receipt status and canonical valuation separately from final manufacturing cost; client-entered provisional value is not authoritative.
- Do not present MRP suggestions as authorized production or purchase orders until explicitly firmed.
- Show forecast consumption and accepted sales demand in the MRP explanation; never add them blindly.
- For the canonical overlap fixture, show 1,000 forecast, 600 consumed by accepted Sales, 400 remaining forecast, and a 1,000 gross/net requirement before supply. With no finished stock the Production suggestion is 1,000 PACK; with 600 available finished stock it is 400 PACK. The 100-unit EA Purchase supply is a separate component supply and must not satisfy finished-product demand.
- Use explicit retry/idempotency keys for shop-floor and material/output commands.

## Development seed scenarios available for UI evaluation

- Atlas and Delta each have a main plant, active finished-goods BOM and approved production definition.
- `MO-SEED-0001` contains 100 ordered, 60 completed, component material with 100 issued / 60 consumed / 40 returned, a pending provisional 60-unit output, planned/actual cost facts, and an explainable variance.
- MRP seed contains 1,000 forecast PACK units with 600 consumed by accepted demand, 400 remaining forecast, and a 1,000-unit gross/net planned production requirement before stock. Authenticated source refresh additionally proves one Sales source demand and one approved Purchasing supply per tenant. Runtime A/B/C proofs are recorded in the coverage matrix.

## Not ready for frontend acceptance yet

The backend checkpoint remains `NOT COMPLETE` until the remaining MF27–MF35 and MF45–MF47 integrations, non-empty source fixtures, linked genealogy/output recovery, and authenticated isolated acceptance tests are delivered. Frontend work may use the stable master-data, order/material/cost/planning, operation, close-readiness, and capacity endpoints for contract exploration, but must not treat the module as production-ready. The final backend runtime evidence is recorded in `MANUFACTURING_BACKEND_COVERAGE_MATRIX.md`.
