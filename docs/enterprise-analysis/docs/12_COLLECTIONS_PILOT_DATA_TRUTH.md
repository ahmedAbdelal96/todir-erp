# 12 — Collections Pilot: Source-of-Truth Research, Rules & Acceptance

**Status:** leading PILOT CANDIDATE (not committed commercial priority). **Pilot 0** = deterministic AR/collection hygiene. **Pilot 1** = statistical forecast only if evidence and commercial value justify it. This plan is grounded in repo's `Subledger`, `Payments`, `Banking`, `Invoicing`, not in hypothetical already-trained AI.

## 1. Primary decision questions

1. At selected business as-of, which customers have overdue **collectible** balances, by currency and legal entity?
2. What amount has been actually **received** versus allocated vs credited/written off, without confusing accounting settlement with cash?
3. Which completed customer payments remain unallocated or partly allocated, and for how long (with reliable timestamps)?
4. Which source data are inconsistent (backdating, incomplete bank confirmation, missing relationship, reversal, unexpected date ordering)?
5. Which operational follow-up, if any, is justified based on transparent evidence—not a made-up probability?

## 2. Source discovery targets

| Repo source (pinned SHA) | Existing fields / behavior observed | Do not assume |
|---|---|---|
| `backend/DbMigrator/Database/bootstrap/subledger-schema.sql` | `party_open_item` with `document_date`, `posting_date`, `due_date`, original/settled/open; `application_batch` with `application_date`, `settlement_date`, `created_at`, `confirmed_at`; applications/reversals | `application_date` equals real cash date |
| `backend/Modules/Subledger/Repositories/SubledgerRepository.cs` | `GetPartyBalanceAsync`, `GetAgingAsync`, `GetStatementAsync`, as-of SQL, partial/reversal/write-off logic | Portfolio-wide historical export already exists, or true bi-temporal replay is proven |
| `backend/DbMigrator/Database/bootstrap/payments-schema.sql` | `payment_date`, `posting_date`, nullable `value_date`, `completed_at`, `reconciliation_state` | `value_date` universally populated or bank-confirmed |
| `backend/Modules/Payments/Services/PaymentsService.cs` | `ApplyToPaymentAsync` maps authorized target invoice IDs and user-provided application/settlement dates | allocation happens at same instant as receipt |
| `backend/Modules/Banking/Repositories/BankingRepository.cs` | reconciliation sessions, bank claims, forecast versions | Bank statement confirmed date mapped to payment value date in all paths |
| `docs/modules/10_...SUBLEDGER...md`, `12_...PAYMENTS...md`, `25_...BANKING...md` | domain specs | All semantics implemented/tested or live-quality |

## 3. Event timeline example — WHY the dates cannot be collapsed

**Illustrative only:** invoice due Oct 10; bank transfer appears Oct 21; payment completed Oct 22; user allocates receipt against invoice Nov 2 with `application_date=Oct 20`; analyst runs report Nov 3.

- **Due-date delay:** bank value/receipt date vs invoice due date (11 days if validated bank date Oct21); distinguish amount-weighted partials.
- **Allocation latency:** system completion-to-allocation confirmation (Oct22 to Nov2 = 11 days), **not customer paying delay**.
- **As-of (economic):** query valid-time can count backdated application at Oct20.
- **Known-at (historical forecast):** on Oct25 application wasn't known until Nov2; temporal backtest MUST NOT see it.
- `settlement_date` may be used for FX accounting context and must **not** be uncritically used as bank receipt date.

## 4. Cash and noncash taxonomy — separate before building metrics

| Event type | Cash receipt? | Effect on AR and scoring |
|---|---|---|
| Completed customer cash receipt with valid proof | Possibly yes, per payment method; needs policy | Reduce cash collection remaining when allocated; count as receipt with evidence/date confidence |
| Bank transfer imported with reconciliation evidence | Potentially yes after reconciliation lifecycle verified | Stronger evidence class than manually entered payment |
| Provider/card settlement pending | Not automatically bank cash | Separate provider/settlement states |
| Customer advance before invoice | Cash may exist; not invoice-specific yet | Track unapplied credit, avoid inventing invoice payment date |
| Credit note | **No cash** | AR reduction; exclude from cash performance numerator |
| Write-off / allowance | **No cash** | AR adjustment, separate negative collection outcome |
| Application reversal | Reverses prior allocation | Correct historical effective and known-at views, no double count |
| Refund | Negative movement / potential contra receipt | Not positive collection; owner lifecycle dictates sign |
| Opening balance imported | No pre-go-live receipt proof | Allow descriptive balance but label behavioral history insufficient |
| Backdated transaction | Accounting effect may precede entry | Preserve both effective and known times, no future info leakage |

**Taxonomy is an APPROVAL GATE**: source owners/accounting SME validate classification. One generic `payment_date` fallback should not silently stand in for proven bank cash.

## 5. Deterministic Pilot 0 outputs and formulas to certify

### 5.1 Outstanding and overdue by party

Use owner Subledger as-of calculation with credit/debit polarity and valid applications, reversals and write-offs. For each currency separately:

`outstanding(asOf,knownAt) = recognized original receivable - effective applied amounts + effective reversal amounts - approved write-offs`.

This is **conceptual**: validate owner polarity and write-off semantics before implementing. Never sum currencies without explicit conversion policy. Aging buckets respect due date and current partial balance; predefined buckets documented with boundary tests (due today vs overdue tomorrow).

### 5.2 Cash collection by customer and invoice

Sum eligible completed receipt allocations linked to payment source, explicitly excluding credit notes/writeoffs, with amount-weighting for partials. Track `UnverifiedReceiptDate` separately. **An unallocated payment is collected at customer level but cannot be attributed to a specific invoice without evidence.** Do not force allocation to a guessed invoice.

### 5.3 Allocation lag

`confirmation_time_of_application − payment_completion_time` where both trustworthy; negative or missing timestamps become warnings/data-quality cases. Not the same as client delay.

### 5.4 Recommendation rules (initial candidates)

- `COLLECTION_REVIEW_OVERDUE`: persistent overdue exposure beyond configurable, explainable policy; recommend human follow-up with due invoices.
- `COLLECTION_REVIEW_UNALLOCATED`: completed receipt still unallocated at known-at; review allocation workflow.
- `COLLECTION_REVIEW_DATE_ANOMALY`: date order inconsistent; request accountant validation, not credit restriction.
- No model-predicted collection date, automatic blocking or payment promise certainty in Pilot 0.

## 6. Data readiness audit — done BEFORE approving pilot

- Use **sanitized/approved actual client data** if available, else controlled fixtures and label readiness unknown for real deployment.
- Percentage of completed customer receipts with meaningful/verified value/booking dates, per payment method.
- Percentage with reconciliation evidence; classify unmatched vs reconciled and how status updates happen.
- Payment→open-item→application linkage completeness and count of unallocated credits.
- Date gaps: payment posting vs completion vs value vs allocation confirmation; histograms, missing/negative delays.
- Opening-balance share, imported historical coverage, number of closed invoices per customer, currency/branch consistency.
- Write-off/reversal/credit note rates; whether a source amount can move or be corrected after captured cutoff.
- Backdated transactions and exact known-at reconstruction; compare historical computed totals to period-end documented reports.
- Runtime performance of portfolio extraction on representative large entity under ongoing operational load.

**Data thresholds:** Do not invent. Report distribution/coverage and present explicit acceptance proposal to finance/product owner. Static schema existence is **not** evidence of customer data quality.

## 7. Correctness test fixtures — minimum

1. Invoice with due date, paid in full on time, allocated same day.
2. Partial receipts before/after due date, remaining balance and weighted delay.
3. Completed advance without invoice allocation: cash vs invoice collection separated.
4. Credit note offsets invoice: no cash counted.
5. Write-off and its approval: no cash counted.
6. Application reversed after cutoff; economic and known-at views differ.
7. Payment reversed/refunded and allocation rules obey owner lifecycle.
8. Backdated invoice entered later, source not known at earlier backtest cutoff.
9. Foreign currency and base currency with owner FX context; no cross-currency sum.
10. Opening balances without historical pre-go-live cash dates.
11. Completed payment with null value date, unmatched bank statement: marked unverified, not silently assumed cash verified.
12. Multiple legal entities/branches + narrow role cannot see other scope.
13. Customer receipt split across 2+ invoices, same receipt not counted twice in total.
14. Posted vs draft invoice/credit-note states; no false AR exposure.
15. Filter changes do not compute until deliberate user POST; two same POST retries create one run.
16. Worker restart during calculation restores job; results and recommendations exactly one published set.

## 8. Gate to statistical Pilot 1 (future)

- Temporal train/validation/test splits with no future-information leakage; compare against baseline simple policy (due dates / recent customer history); precision/recall/calibration or proper score as appropriate to outcome. Customer sample size and behavior variability verified.
- Define prediction **target** and truth timestamp unambiguously (e.g., probability receipt confirmed within 30 days, not `application_date` coincidence). Validate segmented by customer size, amount, season and method.
- Establish prediction robustness, false positive/negative business cost, model drift, explainability and fallback to deterministic signals.
- If Python materially outperforms a maintainable .NET approach given real metrics, propose ADR and isolated compute integration; otherwise remain .NET only.

## 9. Pilot completion criteria

- Data semantics signed off by finance/product owners.
- Independent case fixtures and reconciliations pass for all exceptions.
- Clear human-recognizable Arabic UX, typed filters and historical versioned reports.
- Useful recommendation rules with explicit source evidence and review states; no false causal claim.
- No material operational performance regression under mixed workload, plus security/RLS/permission gates.
- Customer review confirms decision value; failing pilot triggers alternative comparison, not inflated claims.
# P0 evidence supplement

Collections remains `PROPOSED / NOT APPROVED`. The source-grounded gaps around portfolio reads, payment/bank timing, allocation, and temporal reconstruction are recorded in [`P0-03_SOURCE_OWNERSHIP_AND_DATA_TRUTH.md`](../evidence/p0/P0-03_SOURCE_OWNERSHIP_AND_DATA_TRUTH.md).

