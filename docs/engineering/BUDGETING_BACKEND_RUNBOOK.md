# Budget Control Backend Runbook

This runbook covers the BC01 controls currently implemented in the Planning backend. It is not production certification and does not replace owner-module reconciliation procedures.

## Reconcile or rebuild

Use the authorized endpoint:

`POST /api/v1/planning/budget-control/reconcile`

with `{"bucketId":"<optional-id>","rebuild":false}` for a read-only owner reconciliation, or `rebuild:true` for a bounded correction/rebuild. The caller needs the Planning budget-management permission and an active module lifecycle. The operation loads owner facts through public Purchasing and Ledger contracts, compares Actual/Commitment/Precommitment by legal entity, period, metric and currency, and creates a durable `budget_control_owner_reconciliation` result with a source watermark. The outer operation also creates `budget_control_reconciliation_run` evidence with `Running`, `Succeeded`, or `Failed` state, bucket count, discrepancy amount, and failure detail.

When `rebuild:true`, the owner-fact result is applied to the bucket aggregates in the same Planning transaction. Source reservations remain the source of open commitment/precommitment and are not copied into both the bucket aggregate and reservation ledger. The endpoint is tenant-scoped and protected by FORCE RLS. Never repair budget facts with ad-hoc SQL in a live environment. A Planning-only rebuild from historical facts is not sufficient for strict activation.

## Strict activation

`Block` policy decisions require an active policy, an effective date, a published plan version, `opening_reconciled=true`, and a successful zero-discrepancy owner result for the bucket. A future `strict_activation_at` also keeps the policy from blocking before its approved activation time. Shadow mode records a non-blocking decision while operational evidence is collected. Source changes must be reconciled again; the current implementation does not yet automate watermark invalidation or approval of activation.

## Overrides

Request a source-bound override through `/budget/overrides`, then use the Platform Approval decision route. The override is bound to tenant, bucket, source module/document/line/version, currency, amount and optional expiry. It does not change the Budget or approve the source document. Usage is row-locked, bounded by `used_amount <= amount`, and rolled back with the source transaction. Reuse for another source or amount is rejected.

## Financial corrections

Posted supplier invoices consume the Purchasing reservation once during Invoicing finalization. Supplier credit notes and canonical Expense reversals create immutable `Reversed` correction facts linked to the original fact; retries reuse the correction identity. Unposted invoice drafts do not consume Actual.

## Recovery limits

If reconciliation fails, retain the failed run and correct the owning source workflow through its supported command, then rerun reconciliation. Do not mark a strict policy reconciled manually. Expense reversal failures remain in `ReversalPending` with a durable `expense_budget_reversal_intent`; retry the owner reversal command to reuse the intent. Full PR conversion vector, receipt/return lineage, Ledger YTD provider, external Outbox/Inbox replay, and complete multi-source recovery remain open BC01 evidence gates.
