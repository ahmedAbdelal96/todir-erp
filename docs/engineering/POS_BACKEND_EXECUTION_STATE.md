# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS — backend implementation is hardened; remaining acceptance gaps are explicitly recorded below
- Branch: `dev`
- Baseline: `ee645dfb818f1be04267ac49cf72f29c75b949db` plus the scoped final certification fixes in this working tree
- Last meaningful change: fixed failed-card replacement so failed provider tenders are terminal and a replacement cash tender can reopen the payment boundary; serialized checkout/session-close locking; aligned Payments number allocation with its legal-entity uniqueness key; fixed concurrent POS receipt-number collisions; seeded a runtime `pos_supervisor`; and changed the seeded serial profile to enforced reservation so duplicate serial allocation cannot bypass the POS reservation boundary.
- Next executable action: close the explicitly partial PS31/PS32/PS37 evidence gaps before declaring the backend release gate complete. Frontend, physical-device, country-specialist, and statutory hardware acceptance remain outside this backend mission.
- Fresh evidence currently valid: disposable `pos_accept_cert_20261107v` completed the 151-script bootstrap and Development seed, then passed all 14 POS acceptance tests after the final return-concurrency fix. The same disposable database passed Payments PostgreSQL 4/4, POS PostgreSQL 3/3, API composition 3/3, POS unit 15/15, architecture 21/21, RabbitMQ 2/2, backend build, formatting, and `git diff --check`.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PASS | Active Catalog variant, Pricing resolution, UOM/serial validation, Tax calculation, immutable line snapshots, durable checkout state, and restart-safe recovery are implemented and exercised |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PASS for cash/card/split and failed-tender replacement | Stocked cash checkout, reference-provider card webhook/recovery, split Cash+Card recovery, and failed-card → replacement-cash recovery are persisted and automated with idempotent retries; failed card and captured replacement cash remain distinct and downstream effects occur once. |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL — traceability now automated; override approval evidence remains focused | Cash expected-balance, session close, restock return, cash refund, credit note, linked exchange, customer credit sale, and lot/serial sale/return reconciliation are automated; manager override expiry/replay and weighted-goods evidence remain to be added. |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL — offline automated; fiscal/hardware evidence remains contract-level | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; signed offline package download, envelope acceptance, replay, and cash processing are automated against PostgreSQL. Fiscal/hardware adapter runtime acceptance remains outside the local backend fixture. |
| PS29 | Multi-terminal concurrency | PASS for covered backend races; load evidence remains PS32 | Checkout/session-close row locking, two concurrent authenticated recovery calls, two concurrent same-serial sales converging to one economic winner, and two concurrent returns against one sale line are automated. The losing return receives stable `409 pos.concurrent_operation_conflict`/business-conflict behavior and persisted inventory effects remain single. |
| PS30 | Operational reporting | PASS — route and persisted runtime evidence | Canonical unscoped and legal-entity-scoped report routes are exposed, bounded, stably ordered, indexed, and the acceptance suite reads persisted sales, payment-mix, and session-summary facts from PostgreSQL. |
| PS31 | Security / privacy / PCI | PARTIAL — runtime role/isolation proof | Forced RLS and resource-scoped authorization are covered by PostgreSQL tests plus seeded cashier/manager/supervisor runtime permission checks, anonymous denial, same-tenant unauthorized denial, cross-tenant register denial, and card webhook signature proof. The seeded development fixture has one POS branch per tenant, so explicit cross-branch and card-data redaction assertions remain required. |
| PS32 | Performance / load | PARTIAL — bounded local evidence only | Seeded report plans measured 0.346 ms sales and 0.128 ms returns execution with bounded reads and indexes. This is not store-scale checkout/load certification. |
| PS33 | Migration / go-live | PASS | Fresh disposable bootstrap and Development seed completed all 151 ordered scripts; applied baseline checksums remained unchanged and exchange forward-fix ordering is deterministic |
| PS34 | Runbooks / recovery | PASS | Recovery runbook covers unknown payment, internal completion failure, inventory conflict, fiscal/hardware failure, offline expiry/conflict, cash/session reconciliation, return/exchange disagreement, projection rebuild, and restore validation |
| PS35–PS36 | Cashier touch / keyboard / scanner acceptance | EXTERNAL — backend ready | API contracts and backend states are ready; physical UX and input-mode acceptance belong to frontend/retail operations ownership |
| PS37 | Store manager acceptance | PARTIAL — backend routes plus session/role runtime evidence | Sessions, overrides, returns, cash variance approval, checkout recovery, and offline conflict resolution are exposed; the acceptance suite proves start-close → cash-count → close, manager report authorization, manager discount approval/expiry, and seeded supervisor scope on real PostgreSQL. A persisted manager variance-approval and offline-conflict decision journey remain. |
| PS38 | Finance / inventory reconciliation | PARTIAL — cash/card/return/exchange/credit/lot/serial path automated | The acceptance suite asserts cash and provider-card recovery with captured payment; the cash path asserts confirmed Inventory issue, finalized Invoicing document, posted Ledger fact, and return/refund/credit-note completion; split exchange, credit sale, and lot/serial sale-return persist separate POS/payment/document/inventory facts. Concurrent inventory reconciliation remains. |
| PS39 | Country compliance acceptance | EXTERNAL — backend seam ready | Country-neutral fiscal/eReceipt and tax-provider contracts are implemented; statutory country specialist sign-off is outside backend ownership |
| PS40 | Cross-module end-to-end acceptance | PARTIAL — cash/card/split/credit/return/exchange/offline/traceability automated | Cash, reference-provider card recovery, split Cash+Card, credit sale, cash return/refund/credit-note, linked exchange, signed offline cash replay, and lot/serial sale-return paths run through real authenticated API/PostgreSQL tests with persisted downstream effects; concurrent inventory journeys remain. |
| PS41 | Backend release gate | PARTIAL | Current focused gates pass: builds, POS unit 15/15, POS PostgreSQL 3/3, architecture 21/21, broker 2/2, API composition 3/3, and `git diff --check`. Full PS00–PS41 acceptance is not claimed until the missing automated journeys and role/concurrency/load evidence are added. |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.

## Final local verification record for this checkpoint

- `PosAcceptanceIntegrationTests` on fresh disposable `pos_accept_cert_20261107v`: PASS, 14/14, after the final POS database-conflict response fix and assertion correction. The suite includes the concurrent return race: one accepted return, one stable conflict response, one persisted return line, and one persisted Inventory receipt.
- Payments PostgreSQL integration: PASS, 4/4. POS PostgreSQL integration: PASS, 3/3. API composition: PASS, 3/3. POS unit tests: PASS, 15/15. Architecture tests: PASS, 21/21. RabbitMQ broker tests: PASS, 2/2.
- Backend solution build: PASS, 0 warnings / 0 errors. Full-solution `dotnet format --verify-no-changes`: PASS. `git diff --check`: PASS.
- `dotnet build backend/Api/Api.csproj --no-restore`: PASS, 0 warnings / 0 errors.
- `dotnet build backend/DbMigrator/DbMigrator.csproj --no-restore`: PASS, 0 warnings / 0 errors.
- POS unit tests: PASS, 15/15.
- Architecture tests: PASS, 21/21.
- POS PostgreSQL integration tests: PASS, 3/3, including forced RLS, tenant visibility, seeded exchange permission, report indexes, and register/legal-entity ownership.
- RabbitMQ broker gate: PASS, 2/2. Worker startup declared `pos-recovery` and the platform outbox endpoint against the real local broker. The disposable certification database contains legacy pending recovery messages with missing UOM data from earlier negative fixtures; those messages correctly fault rather than being silently completed and were not used as a success claim.
- API composition/OpenAPI gate: PASS, 3/3. OpenAPI contains the canonical POS route set; local API health, authenticated login, canonical/scoped register reads, and anonymous denial were verified at `http://127.0.0.1:5080`.
- Fresh disposable bootstrap: PASS, 151 ordered scripts plus Development seed for Atlas and Delta. Exchange forward-fix ordering was corrected in the migrator without changing an applied baseline checksum.
- POS report query evidence: sales page execution 0.346 ms and returns page execution 0.128 ms on the disposable seeded dataset, with bounded `LIMIT pageSize + 1` reads and report-order indexes present.
- POS runtime performance evidence: 20 catalog barcode requests p50 17.7 ms / p95 19.6 ms; 20 page-size-25 sales report requests p50 12.5 ms / p95 14.2 ms on the local TestServer/PostgreSQL disposable environment, zero request errors. This is bounded runtime evidence, not store-scale certification, and does not satisfy every PS32 benchmark category.

The repository-wide integration invocation was also run once. Its failures are not POS release evidence: several suites require their own disposable database-name and password environment, Catalog replay has a pre-existing unrelated defect, and Payments latency tests reused the POS certification database. No unrelated source was changed for those failures and they do not invalidate the POS-specific gates above.

Backend-owned PS35/PS36 touch, keyboard, scanner, and mouse usability sign-off remains an external product/frontend acceptance boundary. PS39 statutory country-specialist sign-off remains external; POS exposes country-neutral fiscal/tax seams and unit contracts. No frontend or hardware implementation is part of this checkpoint. The backend release gate remains open until the explicitly partial PS29/PS31/PS32/PS37/PS41 evidence is closed; no unsupported completion claim is made.
