# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS — API contract closure and acceptance evidence
- Branch: `dev`
- Baseline: `16fef13961fa5d402a5d79d81981352a5652123b`
- Last meaningful change: closed the canonical POS API route gaps with unscoped register/session/report aliases, cart line/customer/discount operations, canonical checkout/payment-status/return-line routes, and a runtime-authenticated API contract test; corrected the start-close → cash-count → close state transition and preserved line quantities when applying discounts.
- Next executable action: convert the remaining manually exercised cross-module POS journeys into reproducible API/PostgreSQL integration tests and rerun the complete PS29–PS41 acceptance evidence. Frontend, physical-device, and country-specialist acceptance remain outside this backend mission.
- Evidence currently valid: fresh local API runtime returned 200 for authenticated canonical and scoped register reads and 401 for the anonymous request; OpenAPI contract coverage includes the complete documented backend route set; fresh POS PostgreSQL integration is 3/3, POS unit tests are 15/15, architecture is 21/21, API composition is 3/3, and RabbitMQ broker tests are 2/2. The previously recorded cross-module manual scenarios remain useful evidence but are not being relabeled as automated acceptance.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PASS | Active Catalog variant, Pricing resolution, UOM/serial validation, Tax calculation, immutable line snapshots, durable checkout state, and restart-safe recovery are implemented and exercised |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PASS | Stocked cash/card checkout, durable pre-provider drafts, provider-reference persistence, provider-success recovery, stable payment idempotency, UOM validation, source references, return credit notes, mixed-tender boundary proof, and persisted cross-module outcomes |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PASS | Cash expected-balance movements, close blockers/variance approval, quantity-safe returns/refunds, lot/serial references, customer credit validation, discount overrides, completed exchange orchestration, exchange scope protection, and retry-safe completion are implemented and exercised |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PASS | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; offline packages are versioned/expiring/signed; envelope replay validates package/register/device/signature/sequence; manager conflict resolution and durable offline cash-sale processing are implemented |
| PS29 | Multi-terminal concurrency | PARTIAL — implementation plus prior manual evidence | Database uniqueness/locking and prior manual concurrent-idempotency evidence remain valid; a reproducible automated POS API concurrency suite is still required. |
| PS30 | Operational reporting | PASS — route/API contract; runtime data evidence pending automation | Canonical unscoped and legal-entity-scoped report routes are exposed, bounded, stably ordered, and covered by OpenAPI assertions; full persisted report acceptance should be automated. |
| PS31 | Security / privacy / PCI | PARTIAL — runtime contract proof | Forced RLS and resource-scoped authorization are covered by PostgreSQL tests plus anonymous/authenticated register API proof; cashier/manager/cross-branch/cross-tenant role matrix automation remains required. |
| PS32 | Performance / load | PARTIAL — bounded local evidence only | Seeded report plans measured 0.346 ms sales and 0.128 ms returns execution with bounded reads and indexes. This is not store-scale checkout/load certification. |
| PS33 | Migration / go-live | PASS | Fresh disposable bootstrap and Development seed completed all 151 ordered scripts; applied baseline checksums remained unchanged and exchange forward-fix ordering is deterministic |
| PS34 | Runbooks / recovery | PASS | Recovery runbook covers unknown payment, internal completion failure, inventory conflict, fiscal/hardware failure, offline expiry/conflict, cash/session reconciliation, return/exchange disagreement, projection rebuild, and restore validation |
| PS35–PS36 | Cashier touch / keyboard / scanner acceptance | EXTERNAL — backend ready | API contracts and backend states are ready; physical UX and input-mode acceptance belong to frontend/retail operations ownership |
| PS37 | Store manager acceptance | PARTIAL — backend routes ready | Sessions, overrides, returns, cash variance approval, checkout recovery, and offline conflict resolution are exposed through authorized backend workflows; a reproducible role-based API acceptance suite remains. |
| PS38 | Finance / inventory reconciliation | PARTIAL — prior manual evidence only | Prior card recovery and offline cash evidence showed one sale/payment/tender/inventory issue/invoice and balanced ledger effects; the complete persisted assertion set is not yet automated in this repository. |
| PS39 | Country compliance acceptance | EXTERNAL — backend seam ready | Country-neutral fiscal/eReceipt and tax-provider contracts are implemented; statutory country specialist sign-off is outside backend ownership |
| PS40 | Cross-module end-to-end acceptance | PARTIAL — route and prior runtime evidence | Cash, card/provider recovery, mixed tender, credit, returns/refunds, exchange, lot/serial, and offline cash/sync routes are exposed; complete automated cross-module journeys are still required. |
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
