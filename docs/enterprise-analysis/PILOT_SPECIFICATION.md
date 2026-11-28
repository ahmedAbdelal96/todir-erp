# Pilot specification — Collections Hygiene

This specification follows `docs/enterprise-analysis/docs/17_PER_ANALYSIS_SPECIFICATION_TEMPLATE.md`. It is a P2 design contract, not an implementation approval.

```yaml
spec_id: collections-hygiene-v1
status: NEEDS_OWNER_APPROVAL
owner_module: subledger
analysis_code: collections-hygiene
display_name_ar: تحليل تحصيلات العملاء
trigger: explicit_user_run_only
engine: deterministic_dotnet
source_contract: proposed ICollectionsAnalysisReadCapability
result: immutable_published_snapshot
recommendations: deterministic_and_human_reviewed
```

## Decision question and scope

Question: “Which receivable open items or customer concentrations need a human collections follow-up, and what evidence supports that priority?” The pilot covers authorized receivable scope, explicit legal entity/branch filters where supported, due-date aging, open base balance, partial settlement and completeness warnings. It excludes payable collections, credit scoring, payment application, write-off, dunning, customer messaging and autonomous action.

## Definitions and truth table

| Metric | Owner fact | Calculation | Limitation |
|---|---|---|---|
| Open balance | Subledger open/base amount | Sum by approved currency scope | Not revenue or cash. |
| Overdue balance | Due date, open amount, cutoff | Open balance where due date < cutoff | Requires supported cutoff semantics. |
| Partial settlement | Original/settled/open amounts and status | Positive settled and positive open | Owner must define reversal treatment. |
| Concentration | Party identity plus approved balances | Share of scoped open base | No cross-currency aggregation without policy. |
| Completeness | Source watermark/page/error state | Publish warning or block per definition | Partial data never appears as complete. |

## Run and result contract

Filters are validated before enqueue: legal entity/branch scope, customer/party subset, cutoff/as-of date, aging basis, currency policy and page/limit bounds. The run stores a canonical filter snapshot, normalized filter hash, requester, effective-access revision, definition version, source contract versions, requested cutoff, extraction watermark, warnings and idempotency key. Result publication is one transaction and immutable. A new run creates a new result; it never edits the old one.

## Recommendation rule examples

- `OVERDUE_HIGH_BALANCE`: overdue bucket and balance exceed definition thresholds; evidence includes source item references and dates.
- `PARTIAL_SETTLEMENT_FOLLOW_UP`: item is partially settled with remaining balance and no excluded reversal condition.
- `CONCENTRATION_REVIEW`: a party or approved segment exceeds a concentration threshold; impact is shown only as calculated scope share.

Each rule has a version, deterministic inputs, explanation, limit and a `no_action` outcome when evidence is insufficient. Review records accept, reject, defer or request owner action; they never post or alter Subledger.

## P2 Definition of Done

The vertical slice is accepted only when it has: explicit-run API; durable job and retry/cancel path; approved Subledger read capability; deterministic calculation; saved immutable result/history/evidence; recommendation and no-recommendation states; tenant/entity/branch authorization and RLS proof; Arabic RTL frontend route within the owning module; no auto-run/duplicate proof; failure/partial/stale/cancelled states; and performance samples labelled as local pilot evidence.
