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
| MRP netting and pegging | Forecast consumption, accepted-demand netting, firmed-supply exclusion, pegging | Planning run, demand, run, supply, firm endpoints | 1,000 forecast / 600 consumed => 400 residual | Seeded SQL invariant; authenticated API run returned Completed with 2 demand rows and 1 planned supply | PASS |
| Sales/Purchasing source refresh | Sales confirmed remaining demand and eligible Purchasing PO supply are read through owner capabilities; Manufacturing persists planning snapshots without owning source tables | Authenticated `POST /planning-runs/{id}/run` refreshes owner sources before netting | Atlas/Delta canonical Sales and Purchasing seed | Local API run PASS; source queries remain empty for the selected seeded horizon, so non-empty source-line pegging still needs fixture coverage | PARTIAL |
| Operation execution and close readiness | Optimistic row version, per-report idempotency, operation events, predecessor/quality guards, material/output/cost close blockers | Operations, operation action, close-readiness endpoints | Seeded in-progress operation; close is blocked by partial output/cost | Authenticated API: operation completion 200, replay 200 after stale version, close-readiness 200 Blocked, capacity 200 | PASS |
| Capacity read model | Calendar hours, efficiency, planned operation minutes and explicit Unknown/Overloaded state | Plant capacity endpoint | Seeded work center; no calendar yields Unknown | Authenticated API returned capacity row with Unknown state and bounded date range | PASS |
| Genealogy command/read model | Input-event/output linkage with allocation and idempotency constraints | Genealogy create, forward and reverse endpoints | No canonical genealogy event fixture | Persistence and API route build proof only; real linked lot/event acceptance remains | PARTIAL |
| Outbox / RabbitMQ / Inbox | Manufacturing events are published transactionally and consumed with inbox + idempotency | Worker `manufacturing-integration` endpoint | Production output event probe | Real local broker proof: fresh `ProductionOrderStartedV1` reached RabbitMQ and Worker once (`Published`, one Inbox row, one Manufacturing idempotency row, one audit row) | PASS |
| RLS and server authorization | Forced tenant RLS and permission-aware API access | API group requires authorization/rate limit | Two seeded tenants | Runtime query: Atlas=1, Delta through Atlas=0 | PASS |
| Genealogy, shop floor, capacity readiness, external demand projections, close/readiness reporting | Tables/seams exist, but complete command/read-model workflows and all cross-module source consumers are not yet delivered | — | No full acceptance fixture | No complete end-to-end proof | NOT COMPLETE |

## Mandatory local evidence

- `dotnet build backend/Backend.sln /m:1 /nr:false --no-restore`: PASS, zero warnings/errors after the last source change; API and Worker local runtime startup also passed.
- Manufacturing domain tests: 6/6 PASS.
- Sales owner capability tests: 2/2 PASS; Purchasing owner capability tests: 10/10 PASS.
- Architecture tests: 21/21 PASS.
- Isolated DbMigrator migration: 232 ordered scripts PASS, including `manufacturing-schema.sql` and `manufacturing-forward-fix-execution-readiness.sql`.
- Isolated Development Seed: Atlas and Delta completed; manufacturing fixture created for each tenant.
- Development seed regression: fresh disposable database, migration plus seed twice, 1/1 PASS.
- Real local RabbitMQ/Worker: a fresh `ProductionOrderStartedV1` outbox message reached RabbitMQ and the Worker; the outbox row became `Published`, with one `manufacturing-integration` Inbox row, one Manufacturing worker-idempotency row, and one audit row.
- RLS probe: Atlas manufacturing rows visible in Atlas context; Delta rows invisible in the Atlas context.
- Security tests: 7/7 PASS when run against the isolated acceptance database.
- API runtime: `/health` 200, `/openapi/v1.json` 200, 50 Manufacturing paths registered; authenticated production-order, MRP, operation, close-readiness, and capacity paths returned 200 where the seeded state allowed it.
- .NET formatter: `dotnet format backend/Backend.sln --no-restore --verify-no-changes` PASS after formatting the new Manufacturing project.
- `git diff --check`: PASS after the final source/doc changes.

The matrix must be updated when the remaining NOT COMPLETE rows are implemented; it must not be converted to a completion claim from compilation alone.
