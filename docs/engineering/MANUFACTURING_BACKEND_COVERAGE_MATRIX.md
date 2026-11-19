# Module 26 Manufacturing / MRP Backend Coverage Matrix

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This matrix records the current backend delivery evidence. It is deliberately limited to the authorized backend, database, integrations, development-seed, and local acceptance scope; frontend implementation remains a separate mission.

| Capability | Business rule / persistence | API / integration | Seed | Automated / runtime evidence | Status |
|---|---|---|---|---|---|
| Plant and product manufacturing profile | `business_manufacturing.plant`, `manufacturing_product_profile`; tenant/entity keys and RLS | Plant/profile endpoints | Main plant and three profiles per tenant | Isolated migration + seed | PASS |
| BOM versioning | BOM, version, component lines; effective dates and immutable state transitions | BOM/version/approve/activate/supersede endpoints | Active finished-goods BOM | Manufacturing domain tests + migration | PASS |
| Formula/process foundation | Formula versions, ingredients, co-/by-product output lines | Formula/version endpoints | Schema available; no canonical process fixture | Build proof only | PARTIAL |
| Routing and work centers | Routing versions, operations, work centers, calendars | Routing/version/work-center/calendar endpoints | Assembly work center | Build proof + seed | PASS |
| Definition resolver and release snapshot | Deterministic legal-entity/plant/product/date/quantity resolution; immutable snapshot hash | Resolve and production-order endpoints | Approved definition and snapshot | Build + seeded snapshot | PASS |
| Production order lifecycle | Optimistic row version, release/start/hold/resume/complete/close/cancel guards | Production-order endpoints | In-progress partial order | Domain tests; close guard code review | PASS |
| Material conservation | Required/reserved/issued/consumed/returned/scrapped remain distinct; source-key idempotency | Generic material endpoint plus reserve/issue/consume/return aliases | 100 issued = 60 consumed + 40 returned | 6 domain tests; seeded SQL invariant | PASS |
| Inventory orchestration | Manufacturing calls shared Inventory capabilities for reservation, issue, return, and output receipt; no Inventory table writes | Execution service and Inventory contract seam | Inventory-compatible product/warehouse data | API/Worker builds; isolated RLS proof | PASS |
| Partial output and scrap | Output idempotency, provisional/final value fields, scrap facts, partial completion | Output and scrap endpoints | 60/100 output and cost facts | Seeded SQL invariant; output replay guard | PASS |
| Cost facts and variance | Planned snapshot, immutable actual facts, derived cost response, variance persistence | Cost and cost-fact endpoints | EGP planned/actual/variance scenario | Seeded SQL + build | PASS |
| MRP netting and pegging | Forecast consumption, accepted-demand netting, firmed-supply exclusion, pegging | Planning run, demand, run, supply, firm endpoints | 1,000 forecast / 600 consumed => 400 residual | Seeded SQL invariant; planner code build | PASS |
| Outbox / RabbitMQ / Inbox | Manufacturing events are published transactionally and consumed with inbox + idempotency | Worker `manufacturing-integration` endpoint | Production output event probe | Real local broker proof: 2 tenants processed once | PASS |
| RLS and server authorization | Forced tenant RLS and permission-aware API access | API group requires authorization/rate limit | Two seeded tenants | Runtime query: Atlas=1, Delta through Atlas=0 | PASS |
| Genealogy, shop floor, capacity readiness, external demand projections, close/readiness reporting | Tables/seams exist, but complete command/read-model workflows and all cross-module source consumers are not yet delivered | — | No full acceptance fixture | No complete end-to-end proof | NOT COMPLETE |

## Mandatory local evidence

- `dotnet build backend/Backend.sln /m:1 --no-restore`: PASS, zero warnings/errors after the last source change.
- Manufacturing domain tests: 6/6 PASS.
- Architecture tests: 21/21 PASS.
- Isolated DbMigrator migration: 231 ordered scripts PASS, including `manufacturing-schema.sql`.
- Isolated Development Seed: Atlas and Delta completed; manufacturing fixture created for each tenant.
- Real local RabbitMQ/Worker: Manufacturing integration consumer registered and processed the two-tenant output probe exactly once per outbox message; a fresh post-change probe was also published and consumed once (`Published`, one Worker idempotency row, one audit row).
- RLS probe: Atlas manufacturing rows visible in Atlas context; Delta rows invisible in the Atlas context.
- Security tests: 7/7 PASS when run against the isolated acceptance database.
- API runtime: `/health` 200, `/openapi/v1.json` 200, 43 Manufacturing paths registered.
- .NET formatter: `dotnet format backend/Backend.sln --no-restore --verify-no-changes` PASS after formatting the new Manufacturing project.

The matrix must be updated when the remaining NOT COMPLETE rows are implemented; it must not be converted to a completion claim from compilation alone.
