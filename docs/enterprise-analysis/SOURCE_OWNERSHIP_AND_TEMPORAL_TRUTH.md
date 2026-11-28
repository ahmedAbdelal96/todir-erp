# Source ownership and temporal/financial truth

## Collections Hygiene source contract

The first P2 slice must consume an approved typed capability, not `finance_subledger` directly. The minimum proposed capability is:

`ICollectionsAnalysisReadCapability.ListReceivableFacts(scope, filter, cutoff, cursor, limit, cancellationToken)`

Each fact must carry tenant, legal entity, optional branch scope, stable source item id, party reference, source document/reference, direction, document/posting/due dates where supported, transaction amount/currency, open and settled transaction amounts, open base carrying amount/base currency, status, source version, source module, extraction watermark, completeness state and warnings. The contract must be set-based, bounded and cursor-paged; it must not return an unbounded `ListFinancialFactsAsync` shape for Analysis production workloads.

The capability owner remains Subledger. Payments and Invoicing may contribute evidence only through their public capabilities/events; neither becomes an alternative balance source. The Analysis resolver records the exact contract/version and source watermark used for each run.

## Temporal vocabulary

| Time | Meaning | Pilot rule |
|---|---|---|
| Valid/effective time | When a business fact applies. | Use due/posting/application effective dates from the owner. |
| Knowledge/extraction time | When Analysis could observe the fact. | Persist extraction timestamp and source watermark. |
| Document time | Date on invoice/source document. | Display separately; never substitute silently for due date. |
| Due time | Contractual payment due date. | Default aging basis. |
| Settlement/application time | When an amount was applied or reversed. | Required to reconstruct historical open balance. |
| Cutoff | Run’s declared boundary. | Required in the filter snapshot and result. |

The pilot supports current-state analysis and an explicit `asOfDate` only where the owner contract can reproduce the state from immutable application/reversal history. It must reject or mark unsupported any request that needs knowledge-time reconstruction not supplied by the owner. It must never claim bitemporal truth merely because a run has a timestamp.

## Financial invariants

- Base-currency totals may be aggregated only within an explicit currency/conversion policy.
- Transaction-currency groups remain separate when no approved conversion is available.
- Receivable and payable directions are explicit; sign alone is not a business meaning.
- Open amount is not invoice revenue, cash movement, ledger balance, credit exposure approval, or a collection commitment.
- Reversal/correction facts are included according to owner semantics and are visible in provenance/warnings.
- Development Seed is a scenario fixture, not production financial certification.

## Completeness and failure

The source contract returns `complete`, `partial`, or `unavailable` with reason codes. A partial result may be published only when the definition explicitly permits it and the UI prominently labels the limitation. A missing source, unauthorized scope, stale watermark, unsupported date semantics or pagination truncation must not be hidden in a green result.
