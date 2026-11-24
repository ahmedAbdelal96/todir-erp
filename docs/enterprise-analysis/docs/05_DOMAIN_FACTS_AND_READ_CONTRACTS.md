# 05 — Domain Truth, Analytical Read Contracts, Temporal Semantics

**Status:** proposed capabilities + verified sources. **Key decision:** Do not calculate business truth twice. Source module owns facts; Analysis owns derived conclusions.

## 1. Owner fact contract principles

1. **Explicit business meaning:** each numeric field has basis (gross/net, posted/unposted, allocated/cash, inc/ex tax), unit, currency, rounding, owner.
2. **Explicit temporal meaning:** `period_from`/`period_to` measure activity; `business_as_of` measures economic validity; `known_at_utc` measures knowledge availability; `run_started_at` measures processing time. Different columns cannot be treated as interchangeable.
3. **Versioned extraction:** `source_schema_version`, `source_projection_watermark`, `source_fingerprint`, extraction time, completeness count/coverage, and warnings.
4. **Scoped export:** tenant, legal entity, branch/party if supported, row-level permission, authorized fields and dimensions, bounded cursor pages, stable consistent ordering.
5. **No mutable source resurrection:** old analysis references a reproducible immutable fact-set or recorded approved source version; it is not silently rerun from current DB and presented as history.
6. **No arbitrary SQL from filter values.** Queries use typed filters/parameters and legal whitelists for sort/group/filter expressions.

## 2. Financial definition catalog — mandatory before writing code

| Term | Must specify | Invalid shortcut |
|---|---|---|
| Posted sales | Source status, tax basis, credit notes and reversals, legal entity, currency | Sum all orders as revenue |
| Outstanding receivable | Debits - valid applications + reversals - write-offs, as-of and knowledge cutoff | Sum current `open_amount` for past dates |
| Overdue | `due_date < business_as_of` with owner rules and exclusion of negative/credit items | Assume overdue = outstanding without due-date rules |
| Cash received | Payment state/method/value or bank confirmation basis; cash vs credit vs adjustment | Count every application as bank cash |
| Collection delay | Explicit anchor (invoice due/doc/payment value/confirmed) and weighting by amount | `settlement_date - due_date` blindly |
| Payment allocation latency | Payment completion evidence vs application confirmation system-time; may be negative due to backdating | Compare user-entered application_date alone |
| Stock available | Owner availability rule incl. reservations, lot/serial, warehouse and UOM | Use on-hand quantity as always available |
| Production output | Confirmed output, scrap, cost, closure and timing | Treat MRP plan quantity as actual production |
| Cash forecast | Owner's published treasury version versus Analysis what-if projection clearly marked | Publish Analysis projection as approved Banking/FP&A forecast |

## 3. Where facts actually live at inspected SHA

- `SubledgerRepository.GetPartyBalanceAsync` has an as-of-aware branch using applications/reversals/writeoffs, but **existing query is scoped to a party**, not a certified portfolio-wide analytics export.
- `SubledgerRepository.GetAgingAsync` and `GetStatementAsync` exist; no proof of a ready collections bulk contract or all-tenant quality baseline.
- `Subledger` immutable application/reversal records and `Payments` completed payment state exist in code. See `subledger-schema.sql` and `payments-schema.sql`.
- `BankingRepository` owns separate cash-forecast versions. Any analytical liquidity result must identify itself as **analysis suggestion** and not modify Banking's official version.
- `Manufacturing` code folder and manifest exist at pinned SHA. Future cost-analysis needs actual test-backed implementation review, not assumption that every spec workflow works.
- FP&A is specified but its backend directory is absent in inspected module listing; avoid live APIs or reports attributed to it.

## 4. Contract composition example (PROPOSED)

**`CollectionsFactWindowRequest`**: `LegalEntityId`, `BusinessAsOf`, `KnownAtUtc?`, `ActivityFrom`, `ActivityTo`, `CustomerFilter`, `CurrencyCode?`, `Limit`, `Cursor`, `ConsistencyToken?`.

**`CollectionsFactWindowPage`**: `SourceOwner`, `SchemaVersion`, `TenantBoundScope`, `AsOf`, `KnownAt`, `Watermark`, `Completeness`, `NextCursor`, `OpenItemFacts`, `PaymentEvidenceReferences`, `ApplicationFacts`, `ReversalFacts`, `NonCashSettlementFacts`, `QualityWarnings`.

**Never put account credentials, full payment details, personal phone lists, raw bank statement text or provider tokens into generalized AI payloads.** Each analysis requires a minimal purpose-specific projection.

There may be one or more read contracts owned by Subledger, Payments and Banking; the exact shape depends on their current capabilities and database volume. Analysis **may** own a derived cross-domain read model if populated via owner-approved contracts/events, versioned and never authoritative.

## 5. Two temporal axes — illustrative corrected handling

For a fact with business date 2026-10-03 but `created_at=2026-11-06`:

- Report answering **"balance effective on Oct 10, as known today"** may include the backdated fact according to owner accounting rules.
- Backtest asking **"what could manager know on Oct 10?"** must exclude the fact recorded Nov 6, even though business date is Oct 3.
- An event may have **three** timestamps (payment completed, value date, imported bank statement) and later state changes; using `created_at` alone is insufficient for every fact. Determine `knowledge_time` and finalization evidence **per fact type**.

No source count in this document proves complete bitemporal history. **Gate:** controlled fixtures including backdated invoices, partial receipts, delayed allocations, reversals and corrections; compare known-at snapshots and actual financial reports.

## 6. Provenance: snapshot and fingerprint

An AnalysisRun requires: `definition_version`, `normalized_filter_hash`, `scope_hash`, `source_watermark(s)` per owner, `fact_contract_versions`, `known_at_utc`, `source_data_quality`, `engine_version`, `started_at_utc` and `completed_at_utc`. Store hashes/opaque owner references and an **appropriately minimal retained evidence snapshot** when replay needs to remain possible. Keep financial/PII details under role-aware retention policy.

Important: a `watermark` alone does not guarantee multi-owner atomic consistency; snapshot assembly must specify isolation/consistency method. Alternatives: owner-published immutable cutoffs, bounded read snapshot with validation/refresh policy, or explicitly partial cross-owner consistency with warning. Do not falsely call a cross-module extract a single transaction snapshot without proving it.

## 7. Read-model implementation and load strategy

- First prefer existing owner read models **if they include correct dates and scopes**.
- If needed, the owner publishes an optimized analytics export (per-entity, cursor, validated projections, stable sort and indexes) not a public unrestricted table.
- For wide portfolio analysis use **set-based SQL inside the owning module**, not a loop over each customer, and collect only required columns.
- Use streaming/chunked writes to a bounded fact store; define dedupe identities and update strategy for corrections.
- Read replicas are future capacity option: replica lag and as-of cutoff must be visible, and security/routing enforced. Do not blindly assume read replica guarantees immediate consistency.
- Owner projection and analysis run store have separate retention/update semantics: read projection rebuildable, **published run outputs immutable**.

## 8. Completeness and eligibility gates

Every type declares: required factual fields, source states considered final, share of missing `value_date`, unallocated receipts, unknown cash settlement, cancellation/reversal ratios, missing party lineage, source-history coverage, currency/branch accuracy, and reconciliation variance. The gate outcome is `Ready`, `PartialWithWarnings`, `NotReady`, with reasons and affected metrics. Thresholds are **proposed after observing real data**, not fabricated in this document.

## 9. Required contract tests

- Cross-tenant, legal entity, branch, record scope and scoped role access.
- Cursor consistency with inserts/new transactions; explicit snapshot/knownAt semantics.
- Partial cash vs noncash receipts and refunds correctly classified.
- Idempotent rebuild and no duplicate fact IDs.
- Reversals before/after cutoff and backdated knowledge-time replay.
- No integer overflow, floating money values or FX mixing; amounts use decimal and owner rounding rules.
- Performance bound to realistic row counts and no N+1 calls.

**Source:** `backend/Shared/Contracts/SubledgerContracts.cs` currently declares per-party balance, aging, statement and list contracts; any new bulk facts capability is **proposed**, not existing.
