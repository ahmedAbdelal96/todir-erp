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
| BK07 | PaymentAccount compatibility | migration bridge and Payments capability call | PASS |
| BK08 | Tenant/legal-entity scope | composite keys, RLS, API guard | PASS |
| BK09 | Statement import | idempotent import endpoint | PASS |
| BK10 | Statement line evidence | line table, hash, paginated API | PASS |
| BK11 | Import duplicate protection | unique external line and idempotency key | PASS |
| BK12 | Statement account ownership | composite foreign keys | PASS |
| BK12a | Transaction code mappings and rule-set foundation | persisted mapping/rule-set APIs, RLS, and seeded Development mapping/rule | PASS (foundation) |
| BK13 | Reconciliation session | create/list/get API and persistence | PASS |
| BK14 | Match conservation | locked lines and amount checks | PASS |
| BK15 | Match finalization/reopen | state endpoint with version | PASS |
| BK16 | Claim cardinality | claimed amount check and row locks; 1:1/1:M/M:1/M:M automated cardinality evidence still open | PARTIAL |
| BK17 | Cash count denominations | calculated count and denomination rows | PASS |
| BK18 | Cash count approval/finalization | state endpoint and version | PASS |
| BK19 | Cash count difference | persisted expected/count/difference | PASS |
| BK20 | Custodian scope | custodian id and tenant/entity scope | PASS |
| BK21 | Cash position read model | projection table and API | PASS |
| BK22 | Staleness visibility | `IsStale` derived from projection age | PASS |
| BK23 | Forecast version/item lifecycle | forecast tables, version/item API, seed, and focused persistence test | PASS |
| BK24 | Multi-currency storage | currency checks and USD seed | PASS |
| BK25 | FX seam | currency policy and projection basis seam | PASS |
| BK26 | Payment eligibility capability | Banking capability used by Payments | PASS |
| BK26a | Internal transfer proxy | Banking route delegates to Payments capability and records scoped transfer reconciliation projection | PASS (foundation) |
| BK27 | RLS enforcement | forced policies and finalization | PASS |
| BK28 | Permission guard | legal-entity permission checks on routes | PASS |
| BK29 | Sensitive data protection | masked identifiers and secret omission | PASS |
| BK30 | Audit/event seam | manifest event declarations; full authenticated audit evidence still open | PARTIAL |
| BK31 | Outbox seam | manifest integration contracts; post-commit banking event proof still open | PARTIAL |
| BK32 | API pagination | account/statement/line/reconciliation list pages | PASS |
| BK33 | API business errors | stable Banking error codes and ProblemDetails | PASS |
| BK34 | Worker composition | Worker references/registers Banking; feed/parser replay worker acceptance still open | PARTIAL |
| BK35 | Migration ordering | fresh disposable migration | PASS |
| BK36 | Legacy compatibility | stable ids and PaymentAccount FK | PASS |
| BK37 | Development seed | Atlas/Delta BankingSeed | PASS |
| BK38 | Seed idempotency | second run passes | PASS |
| BK39 | Statement fixture | four seeded statements per tenant | PASS |
| BK40 | Reconciliation fixture | seeded open reconciliation per tenant | PASS |
| BK41 | Cash fixture | seeded submitted cash count per tenant | PASS |
| BK42 | Forecast fixture | seeded published forecast per tenant | PASS |
| BK43 | Focused automated tests | BankingTests 6/6 against disposable PostgreSQL after latest changes | PASS |
| BK44 | API build | `dotnet build Api` 0 warnings/errors | PASS |
| BK45 | Worker/migrator build | both builds pass | PASS |
| BK46 | Acceptance evidence and handoff | handoff and checkpoint updated; final cross-module acceptance remains open; performance certification UNVERIFIED | PARTIAL |

## Explicit open Build Now evidence

The following canonical Build Now requirements are not marked PASS until credible implementation and tests exist: transaction-code mapping and persisted matching-rule management; full 1:1/1:M/M:1/M:M/partial matching evidence; unknown-receipt orchestration into Payments; bank fee/interest/direct-debit accounting events; provider settlement and POS handoff reconciliation; internal transfer/in-transit reconciliation; cheque visibility; multi-currency/FX calculation contract; cash-to-GL/report contracts; authenticated legal-entity and tenant isolation HTTP acceptance; worker feed/idempotency replay; migration/opening reconciliation; and runbooks.
