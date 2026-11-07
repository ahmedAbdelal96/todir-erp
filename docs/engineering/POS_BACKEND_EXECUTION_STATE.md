# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: COMPLETE — backend-owned acceptance gates passed; frontend, physical-device, and statutory country acceptance remain outside this mission
- Branch: `dev`
- Baseline: `2093b50` plus the final POS closure changes in this working tree
- Last meaningful change: corrected double-counting of opening float in expected cash; made manager discount approvals one-time and audited; added audited manager variance and offline-conflict decisions; added branch-scoped runtime authorization, weighted quantity, provider-burst, offline-burst, and card-redaction evidence; and kept disposable acceptance data isolated from later tests.
- Next executable action: commit and push the verified backend closure to `origin/dev`. Frontend, physical-device, country-specialist, and statutory hardware acceptance remain outside this backend mission.
- Fresh evidence currently valid: disposable `pos_accept_cert_20261107aa` completed the 151-script bootstrap and Development seed, then passed all 15 POS acceptance tests after the final source change. The same final source passed Payments PostgreSQL 4/4, POS PostgreSQL 3/3, API composition 3/3, RabbitMQ 2/2, POS unit 15/15, architecture 21/21, backend build, formatting, and `git diff --check`.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PASS | Active Catalog variant, Pricing resolution, UOM/serial validation, Tax calculation, immutable line snapshots, durable checkout state, and restart-safe recovery are implemented and exercised |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PASS for cash/card/split and failed-tender replacement | Stocked cash checkout, reference-provider card webhook/recovery, split Cash+Card recovery, and failed-card → replacement-cash recovery are persisted and automated with idempotent retries; failed card and captured replacement cash remain distinct and downstream effects occur once. |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PASS | Cash expected-balance, session close, restock return, cash refund, credit note, linked exchange, customer credit sale, lot/serial sale/return, weighted `2.5 KG`, manager override expiry, one-time override replay rejection, and audit evidence are automated against PostgreSQL. |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PASS for backend-owned scope; statutory adapter runtime external | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; signed offline package download, envelope acceptance, replay, cash processing, concurrent duplicate sync burst, and conflict resolution are automated against PostgreSQL. Physical device and statutory provider certification remain external. |
| PS29 | Multi-terminal concurrency | PASS | Checkout/session-close row locking, two concurrent authenticated recovery calls, two concurrent same-serial sales converging to one economic winner, two concurrent returns against one sale line, and bounded concurrent terminal reads are automated. The losing return receives stable `409 pos.concurrent_operation_conflict`/business-conflict behavior and persisted inventory effects remain single. |
| PS30 | Operational reporting | PASS — route and persisted runtime evidence | Canonical unscoped and legal-entity-scoped report routes are exposed, bounded, stably ordered, indexed, and the acceptance suite reads persisted sales, payment-mix, and session-summary facts from PostgreSQL. |
| PS31 | Security / privacy / PCI | PASS | Forced RLS and resource-scoped authorization are covered by PostgreSQL tests plus seeded cashier/manager/supervisor runtime checks, anonymous denial, same-tenant unauthorized denial, cross-tenant register denial, a branch-scoped cashier denied a different branch, signed webhook validation, and audit redaction checks with no PAN/CVV/provider-secret leakage. |
| PS32 | Performance / load | PASS for bounded local certification targets | Final runtime evidence on disposable PostgreSQL: catalog 20-request p95 19.1 ms; product search 10-request p95 23.4 ms; sales report page-size 25 p95 14.4 ms; 20-line large-cart scan p95 76.2 ms; 8 concurrent terminal read pairs 133.2 ms total; provider webhook duplicate burst 8 in 141.9 ms; offline sync duplicate burst 8; zero errors. |
| PS33 | Migration / go-live | PASS | Fresh disposable bootstrap and Development seed completed all 151 ordered scripts; applied baseline checksums remained unchanged and exchange forward-fix ordering is deterministic |
| PS34 | Runbooks / recovery | PASS | Recovery runbook covers unknown payment, internal completion failure, inventory conflict, fiscal/hardware failure, offline expiry/conflict, cash/session reconciliation, return/exchange disagreement, projection rebuild, and restore validation |
| PS35–PS36 | Cashier touch / keyboard / scanner acceptance | EXTERNAL — backend ready | API contracts and backend states are ready; physical UX and input-mode acceptance belong to frontend/retail operations ownership |
| PS37 | Store manager acceptance | PASS for backend-owned scope | Sessions, overrides, returns, cash variance approval, checkout recovery, reports, and offline conflict resolution are exercised through authenticated manager/cashier runtime calls with persisted state, audit entries, and replay rejection. |
| PS38 | Finance / inventory reconciliation | PASS | Cash/card/split/credit/return/exchange/lot/serial/weighted sales assert persisted POS, Payments, Inventory, Invoicing, Ledger, and cash-session effects; concurrent serial inventory sale and concurrent return races converge to one economic winner. |
| PS39 | Country compliance acceptance | EXTERNAL — backend seam ready | Country-neutral fiscal/eReceipt and tax-provider contracts are implemented; statutory country specialist sign-off is outside backend ownership |
| PS40 | Cross-module end-to-end acceptance | PASS | Cash, reference-provider card recovery and duplicate webhook burst, split Cash+Card, credit sale, cash return/refund/credit-note, linked exchange, signed offline cash replay/burst, weighted quantity, lot/serial sale-return, and concurrent inventory journeys run through real authenticated API/PostgreSQL tests with persisted downstream effects. |
| PS41 | Backend release gate | PASS | Fresh bootstrap/seed, POS acceptance 15/15, Payments PostgreSQL 4/4, POS PostgreSQL 3/3, API composition 3/3, RabbitMQ 2/2, POS unit 15/15, architecture 21/21, backend build, formatting, and `git diff --check` all passed after the final source change. |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.

## Final local verification record for this checkpoint

- `PosAcceptanceIntegrationTests` on fresh disposable `pos_accept_cert_20261107aa`: PASS, 15/15, after the final POS source change. The suite includes weighted `2.5 KG`, branch-scoped authorization, manager variance/offline-conflict audit and replay proof, concurrent provider webhook duplicates, concurrent offline sync duplicates, concurrent same-serial checkout, and concurrent return race: one accepted return, one stable conflict response, one persisted return line, and one persisted Inventory receipt.
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
- POS runtime performance evidence: catalog 20 requests p50 17.7 ms / p95 19.1 ms; search 10 requests p95 23.4 ms; sales report page-size 25 p50 12.8 ms / p95 14.4 ms; 20-line large-cart scan p95 76.2 ms; 8 concurrent terminal read pairs 133.2 ms total; provider webhook duplicate burst 8 in 141.9 ms; offline sync duplicate burst 8; zero errors on the local TestServer/PostgreSQL disposable environment.

The repository-wide integration invocation was also run once. Its failures are not POS release evidence: several suites require their own disposable database-name and password environment, Catalog replay has a pre-existing unrelated defect, and Payments latency tests reused the POS certification database. No unrelated source was changed for those failures and they do not invalidate the POS-specific gates above.

Backend-owned PS35/PS36 touch, keyboard, scanner, and mouse usability sign-off remains an external product/frontend acceptance boundary. PS39 statutory country-specialist sign-off remains external; POS exposes country-neutral fiscal/tax seams and unit contracts. No frontend or hardware implementation is part of this backend mission. All backend-owned PS00–PS41 gates are now closed with reproducible evidence.
