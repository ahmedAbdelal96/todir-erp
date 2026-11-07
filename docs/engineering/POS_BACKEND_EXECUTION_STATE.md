# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: COMPLETE — backend-owned scope
- Branch: `dev`
- Baseline: `674720d5ef10b9fbf4d8e10eae8994658a896597`
- Last meaningful change: added branch/legal-entity resource authorization, register/session/cart ownership validation, provider-success payment boundaries, exchange scope checks, report-order indexes, deterministic migration ordering for exchange forward-fixes, and opening-float cash movement initialization with expected-cash alignment.
- Next executable action: none for the backend-owned POS scope. Frontend, physical-device, and country-specialist acceptance proceed through their owning teams.
- Evidence currently valid: fresh disposable PostgreSQL bootstrap applied 151 ordered scripts and development seed data for Atlas and Delta; the local runtime proved authenticated register access, branch-scoped denial, signed provider-success recovery with exactly one sale/tender/inventory issue/invoice and balanced journal, mixed-tender pending behavior with no premature cash side effect, concurrent checkout/return/offline idempotency behavior, opening-float reconciliation, and report queries using report-order indexes. The focused PostgreSQL POS integration gate is 3/3.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PASS | Active Catalog variant, Pricing resolution, UOM/serial validation, Tax calculation, immutable line snapshots, durable checkout state, and restart-safe recovery are implemented and exercised |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PASS | Stocked cash/card checkout, durable pre-provider drafts, provider-reference persistence, provider-success recovery, stable payment idempotency, UOM validation, source references, return credit notes, mixed-tender boundary proof, and persisted cross-module outcomes |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PASS | Cash expected-balance movements, close blockers/variance approval, quantity-safe returns/refunds, lot/serial references, customer credit validation, discount overrides, completed exchange orchestration, exchange scope protection, and retry-safe completion are implemented and exercised |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PASS | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; offline packages are versioned/expiring/signed; envelope replay validates package/register/device/signature/sequence; manager conflict resolution and durable offline cash-sale processing are implemented |
| PS29 | Multi-terminal concurrency | PASS | Tenant/idempotency checkout serialization, concurrent checkout convergence, return quantity locking, session uniqueness, offline sequence conflict, and replay behavior were exercised against PostgreSQL |
| PS30 | Operational reporting | PASS | Sales, returns, payment mix, session summary, and cash-difference endpoints use bounded reads, stable ordering, legal-entity scope, and report-order indexes; persisted sale/tender/session reconciliation was checked |
| PS31 | Security / privacy / PCI | PASS | Forced RLS, runtime-role isolation, legal-entity and branch resource authorization, cross-branch denial, manager permissions, signed offline envelopes, and provider-token boundary were verified |
| PS32 | Performance / load | PASS — bounded local evidence | Seeded report plans measured 0.346 ms sales and 0.128 ms returns execution with bounded reads and indexes. No synthetic environment-scale benchmark is claimed; normal local POS query certification is complete under the repository's Fast Execution Mode. |
| PS33 | Migration / go-live | PASS | Fresh disposable bootstrap and Development seed completed all 151 ordered scripts; applied baseline checksums remained unchanged and exchange forward-fix ordering is deterministic |
| PS34 | Runbooks / recovery | PASS | Recovery runbook covers unknown payment, internal completion failure, inventory conflict, fiscal/hardware failure, offline expiry/conflict, cash/session reconciliation, return/exchange disagreement, projection rebuild, and restore validation |
| PS35–PS36 | Cashier touch / keyboard / scanner acceptance | EXTERNAL — backend ready | API contracts and backend states are ready; physical UX and input-mode acceptance belong to frontend/retail operations ownership |
| PS37 | Store manager acceptance | PASS — backend workflows | Sessions, overrides, returns, cash variance approval, checkout recovery, and offline conflict resolution are exposed through authorized backend workflows |
| PS38 | Finance / inventory reconciliation | PASS | Real card recovery and offline cash scenarios produced one sale, one payment/tender, one inventory issue, one invoice, and balanced ledger debit/credit evidence; returns/exchanges preserved source links |
| PS39 | Country compliance acceptance | EXTERNAL — backend seam ready | Country-neutral fiscal/eReceipt and tax-provider contracts are implemented; statutory country specialist sign-off is outside backend ownership |
| PS40 | Cross-module end-to-end acceptance | PASS — backend scenarios | Cash, card/provider recovery, mixed tender, credit, returns/refunds, exchange, lot/serial, and offline cash/sync backend scenarios were exercised against PostgreSQL |
| PS41 | Backend release gate | PASS | Builds, POS unit tests 15/15, POS PostgreSQL integration 3/3, architecture 21/21, broker 2/2, API composition 2/2, fresh migration/seed, runtime health/login/register access, and `git diff --check` passed |

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
- API composition/OpenAPI gate: PASS, 2/2. Local API health, authenticated login, and POS register read returned successfully at `http://127.0.0.1:5080`.
- Fresh disposable bootstrap: PASS, 151 ordered scripts plus Development seed for Atlas and Delta. Exchange forward-fix ordering was corrected in the migrator without changing an applied baseline checksum.
- POS report query evidence: sales page execution 0.346 ms and returns page execution 0.128 ms on the disposable seeded dataset, with bounded `LIMIT pageSize + 1` reads and report-order indexes present.

The repository-wide integration invocation was also run once. Its failures are not POS release evidence: several suites require their own disposable database-name and password environment, Catalog replay has a pre-existing unrelated defect, and Payments latency tests reused the POS certification database. No unrelated source was changed for those failures and they do not invalidate the POS-specific gates above.

Backend-owned PS35/PS36 touch, keyboard, scanner, and mouse usability sign-off remains an external product/frontend acceptance boundary. PS39 statutory country-specialist sign-off remains external; POS exposes country-neutral fiscal/tax seams and unit contracts. No frontend or hardware implementation is part of this checkpoint.
