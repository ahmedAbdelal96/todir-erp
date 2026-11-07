# POS Backend Execution State

Canonical pack: `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
Scope: Module 20 backend, PostgreSQL, API, Worker, cross-module contracts, security, seed, and backend acceptance. Frontend and physical hardware UX remain outside this mission.

## Current checkpoint

- Status: IN PROGRESS
- Branch: `dev`
- Baseline: `674720d5ef10b9fbf4d8e10eae8994658a896597`
- Last meaningful change: added branch/legal-entity resource authorization, register/session/cart ownership validation, provider-success payment boundaries, exchange scope checks, report-order indexes, deterministic migration ordering for exchange forward-fixes, and opening-float cash movement initialization with expected-cash alignment.
- Next executable action: complete the final focused backend gates, refresh local runtime evidence, then commit and push only the verified POS backend changes.
- Evidence currently valid: fresh disposable PostgreSQL bootstrap applied 151 ordered scripts and development seed data for Atlas and Delta; the local runtime proved authenticated register access, branch-scoped denial, signed provider-success recovery with exactly one sale/tender/inventory issue/invoice and balanced journal, mixed-tender pending behavior with no premature cash side effect, concurrent checkout/return/offline idempotency behavior, and report queries using report-order indexes. The focused PostgreSQL POS integration gate is 3/3. Full PS00–PS41 certification remains open until the final test and operational evidence gates pass.

## PS00–PS41 matrix

| Phase | Backend deliverable | Status | Evidence |
|---|---|---|---|
| PS00 | Dependency and ownership matrix | PASS | Existing module/API/schema inspection; canonical pack and scoped manuals read |
| PS01–PS04 | POS schema, register/session/cart, calculation seams | PASS | `pos-schema.sql`, forced tenant RLS, runtime role proof, branch/legal-entity register validation, terminal/session/cart invariants, fine-grained route permissions, and focused PostgreSQL integration evidence |
| PS05–PS08 | Catalog/Pricing/Tax validation and checkout state | PARTIAL | Active Catalog variant, Pricing resolution, and Tax calculation are invoked when authoritative line context is supplied; Tax snapshot is persisted on the line; complete checkout ownership is not implemented |
| PS09–PS15 | Cash/card checkout, inventory/document/ledger/payment orchestration, recovery and split tender | PASS | Stocked cash/card checkout, durable pre-provider drafts, provider-reference persistence, provider-success recovery, stable payment idempotency, UOM validation, source references, return credit notes, mixed-tender boundary proof, and persisted cross-module outcomes |
| PS16–PS23 | Cash controls, returns/refunds/exchanges, lot/serial, customer/credit, overrides | PASS | Cash expected-balance movements, close blockers/variance approval, quantity-safe returns/refunds, lot/serial references, customer credit validation, discount overrides, completed exchange orchestration, exchange scope protection, and retry-safe completion are implemented and exercised |
| PS24–PS28 | Fiscal/hardware seams and offline package/sync server contracts | PARTIAL | Country-neutral fiscal/eReceipt and hardware adapter contracts are covered by unit proof; offline packages are versioned/expiring/signed; envelope replay validates package/register/device/signature/sequence; manager conflict resolution and durable offline cash-sale processing are implemented |
| PS29–PS34 | Concurrency, reports, security, performance, migration/go-live, runbooks | PARTIAL | Forced RLS, tenant/idempotency checkout serialization, branch scope enforcement, bounded sales/return/report queries, report-order indexes, deterministic fresh bootstrap, and operational recovery evidence exist; broader concurrency/performance/auth matrix remains |
| PS35–PS41 | API readiness, manager/finance/compliance/E2E/release gates | PARTIAL | API/runtime smoke, 15 POS unit tests, 3 PostgreSQL POS integration tests, architecture tests, fresh seed/bootstrap, and real provider recovery pass; full contract, failure-injection, Worker rehearsal, and release matrix remain |

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

The repository-wide integration invocation was also run once. Its failures are not POS release evidence: several suites require their own disposable database-name and password environment, Catalog replay has a pre-existing unrelated defect, and Payments latency tests reused the POS certification database. No unrelated source was changed for those failures. They remain separate repository-wide certification work and must not be represented as POS PASS.

Backend-owned PS35/PS36 touch, keyboard, scanner, and mouse usability sign-off remains an external product/frontend acceptance boundary. PS39 statutory country-specialist sign-off remains external; POS exposes country-neutral fiscal/tax seams and unit contracts. No frontend or hardware implementation is part of this checkpoint.
