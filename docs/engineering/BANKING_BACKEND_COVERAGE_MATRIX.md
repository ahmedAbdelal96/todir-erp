# Module 25 Banking Backend Coverage Matrix

| ID | Requirement | Evidence | Status |
|---|---|---|---|
| BK00 | Canonical module ownership | `banking-schema.sql`, manifest | PASS |
| BK01 | Treasury account master | account table/repository/API | PASS |
| BK02 | Bank account profile and masking | profile table, masked DTO | PASS |
| BK03 | Cash and petty-cash profile | profile table/cash count API | PASS |
| BK04 | Wallet and settlement profile | profile tables and type mapping | PASS |
| BK05 | Lifecycle and optimistic concurrency | status/update endpoints and version predicates | PASS |
| BK06 | Currency policy and pockets | checks, pocket insert, seed | PASS |
| BK07 | PaymentAccount compatibility | atomic Banking canonical account + Payments projection, stable treasury_account_id, method configuration, and cross-module acceptance | PASS |
| BK08 | Tenant/legal-entity scope | composite keys, RLS, API guard | PASS |
| BK09 | Statement import | idempotent import endpoint | PASS |
| BK10 | Statement line evidence | line table, hash, paginated API | PASS |
| BK11 | Import duplicate protection | unique external line and idempotency key | PASS |
| BK12 | Statement account ownership | composite foreign keys | PASS |
| BK12a | Transaction code mappings and rule-set foundation | persisted mapping/rule-set APIs, RLS, and seeded Development mapping/rule | PASS (foundation) |
| BK13 | Reconciliation session | create/list/get API; book balance is derived from posted Ledger scope and persisted with source snapshot | PASS |
| BK14 | Match conservation | locked lines, Payments-owned eligibility read, amount/direction/currency/state validation, and claim conservation | PASS |
| BK15 | Match finalization/reopen | finalization re-reads Ledger, checks outstanding evidence/adjustments/difference, and uses versioned state transitions | PASS |
| BK16 | Claim cardinality | claimed amount check, row locks, system-item persistence, reconciliation_system_claim ownership, and dedicated partial/cardinality acceptance | PASS |
| BK17 | Cash count denominations | calculated count and denomination rows | PASS |
| BK18 | Cash count approval/finalization | state endpoint and version | PASS |
| BK19 | Cash count difference | persisted expected/count/difference | PASS |
| BK20 | Custodian scope | custodian id and tenant/entity scope | PASS |
| BK21 | Cash position read model | nullable Ledger/external/available balances with explicit basis and source watermark | PASS |
| BK22 | Staleness visibility | `IsStale` derived from projection age; missing projections are not fabricated as zero | PASS |
| BK23 | Forecast version/item lifecycle | forecast tables, version/item API, seed, and focused persistence test | PASS |
| BK24 | Multi-currency storage | currency checks and USD seed | PASS |
| BK25 | FX seam | Payments cross-currency internal-transfer acceptance preserves source/destination currencies, positive FX rate, immutable snapshot, and posted Ledger fact | PASS |
| BK26 | Payment eligibility capability | Banking capability used by Payments | PASS |
| BK26a | Internal transfer proxy and reconciliation | Banking route delegates movement to Payments; source/destination evidence is claimed under row locks and transitions `InTransit` to `Matched` with explicit currencies and versioning | PASS (repository integration evidence) |
| BK27 | RLS enforcement | forced policies and finalization | PASS |
| BK28 | Permission guard | authenticated HTTP acceptance proves restricted Atlas/Delta readers receive own-entity 200, cross-entity and cross-tenant 403, anonymous 401 | PASS |
| BK29 | Sensitive data protection | masked identifiers and secret omission | PASS |
| BK30 | Audit/event seam | candidate approval writes a post-commit `BankOriginatedFinancialEventApprovedV1` outbox event with approving membership; Payments/Ledger consumers retain source evidence | PASS (repository + authenticated HTTP + live replay) |
| BK31 | Outbox seam | POS close, external feed, provider settlement, and bank-originated approval consumers/producers are registered; disposable RabbitMQ replay dispatches and processes a bank-originated event | PASS |
| BK32 | API pagination | account/statement/line/reconciliation list pages | PASS |
| BK33 | API business errors | stable Banking error codes and ProblemDetails | PASS |
| BK34 | Worker composition | Worker registers external-feed, POS-close, provider-settlement, and bank-originated consumers with Inbox idempotency; live replay creates one completed unallocated receipt | PASS |
| BK35 | Migration ordering | fresh/upgrade disposable migration with 229 ordered scripts | PASS |
| BK36 | Legacy compatibility | stable ids and PaymentAccount FK | PASS |
| BK37 | Development seed | Atlas/Delta BankingSeed with Ledger-required reconciliation and source-labelled cash projections | PASS |
| BK38 | Seed idempotency | repeated runs pass and a mutated authoritative cash projection is not overwritten | PASS |
| BK39 | Statement fixture | four seeded statements per tenant | PASS |
| BK40 | Reconciliation fixture | seeded open reconciliation per tenant | PASS |
| BK41 | Cash fixture | seeded submitted cash count per tenant | PASS |
| BK42 | Forecast fixture | seeded published forecast per tenant | PASS |
| BK43 | Focused automated tests | Banking PostgreSQL 15/15, Banking rules 4/4, cross-module Banking→Payments→Ledger acceptance, Payments/Ledger/HTTP regressions, and seed verification pass | PASS |
| BK44 | API build | `dotnet build Api` 0 warnings/errors | PASS |
| BK45 | Worker/migrator build | both builds pass | PASS |
| BK46 | Acceptance evidence and handoff | handoff, checkpoint, runbook, authenticated scope proof, Ledger/payment ownership proof, migration/seed evidence, and live Worker replay evidence are current; heavyweight performance certification is explicitly UNVERIFIED | PASS (performance disposition documented) |

## Explicit open Build Now evidence

All canonical Build Now requirements have credible implementation and acceptance evidence. Unknown receipts create one idempotent completed, unallocated Payments customer receipt from Banking evidence, without inventing a party or creating a Subledger item; live RabbitMQ replay and the capability test prove the path. Internal transfers preserve explicit source/destination currencies and reconcile one-sided evidence as `InTransit` and two-sided evidence as `Matched`. Opening reconciliation requires a posted/locked Ledger opening batch, preserves migration provenance, and finalizes only at zero difference. Bank activity, cash-to-Ledger, cheque visibility, bank fee/interest/direct-debit Ledger posting, and provider/POS settlement handoff contracts are implemented and focused-tested. Operational procedures are documented in `BANKING_BACKEND_RUNBOOK.md`.
