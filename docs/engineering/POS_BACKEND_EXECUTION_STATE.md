# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS — API contract closure and acceptance evidence
- Branch: `dev`
- Baseline: `16fef13961fa5d402a5d79d81981352a5652123b`
- Last meaningful change: converted the cash checkout, persisted invoice/inventory/ledger reconciliation, concurrent recovery retry, report reads, session close workflow, anonymous/unauthorized access, and cross-tenant isolation checks into a real PostgreSQL-backed authenticated API acceptance suite.
- Next executable action: convert the remaining manually exercised split-tender, exchange, credit, lot/serial, and inventory-concurrency journeys into reproducible API/PostgreSQL integration tests and rerun the complete PS29–PS41 acceptance evidence. Frontend, physical-device, and country-specialist acceptance remain outside this backend mission.
- Evidence currently valid: on fresh disposable database `pos_accept_cert_20261107`, `PosAcceptanceIntegrationTests` is 5/5: cash checkout/retry/recovery produced exactly one POS sale, captured tender, invoice, confirmed inventory issue, and posted ledger fact; the same cash journey completed a restock return, cash refund, and credit note; two concurrent recovery calls converged to the same completed checkout; a reference-provider card webhook plus recovery produced exactly one captured card tender and completed Payments record; a signed offline package, envelope, replay, and cash processing converged to one envelope and one checkout; reports returned the persisted sale/payment/session facts; session close persisted counted cash and zero variance; anonymous, same-tenant unauthorized, and cross-tenant requests were rejected. OpenAPI contract coverage includes the documented backend route set; fresh POS PostgreSQL integration is 3/3, POS unit tests are 15/15, architecture is 21/21, API composition is 3/3, and RabbitMQ broker tests are 2/2. The previously recorded cross-module manual scenarios remain useful evidence but are not being relabeled as automated acceptance.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PASS | Active Catalog variant, Pricing resolution, UOM/serial validation, Tax calculation, immutable line snapshots, durable checkout state, and restart-safe recovery are implemented and exercised |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PARTIAL — cash/card automated; split remains manual evidence | Stocked cash checkout and reference-provider card webhook/recovery are now persisted and automated with idempotent retries; durable drafts, provider references, UOM and source links remain covered; split-tender and failed-tender replacement still need automated acceptance. |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PARTIAL — cash/return/refund automated | Cash expected-balance, session close, restock return, cash refund, and credit-note state transitions are automated; exchanges, lot/serial, customer credit, and manager override paths remain implemented but require reproducible API/PostgreSQL acceptance coverage. |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL — offline automated; fiscal/hardware evidence remains contract-level | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; signed offline package download, envelope acceptance, replay, and cash processing are automated against PostgreSQL. Fiscal/hardware adapter runtime acceptance remains outside the local backend fixture. |
| PS29 | Multi-terminal concurrency | PARTIAL — implementation plus targeted API evidence | Database uniqueness/locking and two concurrent authenticated recovery calls converging to one completed checkout are automated; last-stock, serial, return, and multi-terminal session races still require dedicated PostgreSQL callers. |
| PS30 | Operational reporting | PASS — route and persisted runtime evidence | Canonical unscoped and legal-entity-scoped report routes are exposed, bounded, stably ordered, indexed, and the acceptance suite reads persisted sales, payment-mix, and session-summary facts from PostgreSQL. |
| PS31 | Security / privacy / PCI | PARTIAL — runtime role/isolation proof | Forced RLS and resource-scoped authorization are covered by PostgreSQL tests plus anonymous, same-tenant unauthorized, and cross-tenant authenticated register API proof; seeded cashier/manager/cross-branch role matrix and card-data assertions remain required. |
| PS32 | Performance / load | PARTIAL — bounded local evidence only | Seeded report plans measured 0.346 ms sales and 0.128 ms returns execution with bounded reads and indexes. This is not store-scale checkout/load certification. |
| PS33 | Migration / go-live | PASS | Fresh disposable bootstrap and Development seed completed all 151 ordered scripts; applied baseline checksums remained unchanged and exchange forward-fix ordering is deterministic |
| PS34 | Runbooks / recovery | PASS | Recovery runbook covers unknown payment, internal completion failure, inventory conflict, fiscal/hardware failure, offline expiry/conflict, cash/session reconciliation, return/exchange disagreement, projection rebuild, and restore validation |
| PS35–PS36 | Cashier touch / keyboard / scanner acceptance | EXTERNAL — backend ready | API contracts and backend states are ready; physical UX and input-mode acceptance belong to frontend/retail operations ownership |
| PS37 | Store manager acceptance | PARTIAL — backend routes plus session runtime evidence | Sessions, overrides, returns, cash variance approval, checkout recovery, and offline conflict resolution are exposed; the acceptance suite now proves start-close → cash-count → close on real PostgreSQL, but manager-role workflow coverage remains. |
| PS38 | Finance / inventory reconciliation | PARTIAL — cash/card/return path automated | The acceptance suite asserts one cash sale and one provider-card recovery with captured payment; the cash path asserts confirmed Inventory issue, finalized Invoicing document, posted Ledger fact, and return/refund/credit-note completion. Exchange and offline reconciliation still require automation. |
| PS39 | Country compliance acceptance | EXTERNAL — backend seam ready | Country-neutral fiscal/eReceipt and tax-provider contracts are implemented; statutory country specialist sign-off is outside backend ownership |
| PS40 | Cross-module end-to-end acceptance | PARTIAL — cash/card/return/offline automated | Cash, reference-provider card recovery, cash return/refund/credit-note, and signed offline cash replay paths now run through real authenticated API/PostgreSQL tests with persisted downstream effects; mixed tender, credit, exchange, and lot/serial journeys still require automated acceptance. |
| PS41 | Backend release gate | PARTIAL | Current focused gates pass: builds, POS unit 15/15, POS PostgreSQL 3/3, architecture 21/21, broker 2/2, API composition 3/3, and `git diff --check`. Full PS00–PS41 acceptance is not claimed until the missing automated journeys and role/concurrency/load evidence are added. |

## Rulings

- POS will use the existing module composition and transaction patterns; the illustrative namespaces in the design pack are not copied when they conflict with repository conventions.
- POS will expose typed capability seams for modules whose current contracts are narrower; it will not duplicate their source-of-truth tables.

## Final local verification record for this checkpoint

- `dotnet build backend/Api/Api.csproj --no-restore`: PASS, 0 warnings / 0 errors.
- `dotnet build backend/DbMigrator/DbMigrator.csproj --no-restore`: PASS, 0 warnings / 0 errors.
- POS unit tests: PASS, 15/15.
- Architecture tests: PASS, 21/21.
- POS PostgreSQL integration tests: PASS, 3/3, including forced RLS, tenant visibility, seeded exchange permission, report indexes, and register/legal-entity ownership.
- RabbitMQ broker gate: PASS, 2/2. Worker startup declared `pos-recovery` and the platform outbox endpoint against the real local broker. The disposable certification database contains legacy pending recovery messages with missing UOM data from earlier negative fixtures; those messages correctly fault rather than being silently completed and were not used as a success claim.
- API composition/OpenAPI gate: PASS, 3/3. OpenAPI contains the canonical POS route set; local API health, authenticated login, canonical/scoped register reads, and anonymous denial were verified at `http://127.0.0.1:5080`.
- Fresh disposable bootstrap: PASS, 151 ordered scripts plus Development seed for Atlas and Delta. Exchange forward-fix ordering was corrected in the migrator without changing an applied baseline checksum.
- POS report query evidence: sales page execution 0.346 ms and returns page execution 0.128 ms on the disposable seeded dataset, with bounded `LIMIT pageSize + 1` reads and report-order indexes present.

The repository-wide integration invocation was also run once. Its failures are not POS release evidence: several suites require their own disposable database-name and password environment, Catalog replay has a pre-existing unrelated defect, and Payments latency tests reused the POS certification database. No unrelated source was changed for those failures and they do not invalidate the POS-specific gates above.

Backend-owned PS35/PS36 touch, keyboard, scanner, and mouse usability sign-off remains an external product/frontend acceptance boundary. PS39 statutory country-specialist sign-off remains external; POS exposes country-neutral fiscal/tax seams and unit contracts. No frontend or hardware implementation is part of this checkpoint. The backend is not marked complete while the automated cross-module, role/isolation, concurrency, and load evidence listed above is still absent.
