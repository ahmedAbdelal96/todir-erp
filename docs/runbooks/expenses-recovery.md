# Expenses Backend Recovery Runbook

This runbook is for Finance and platform operators. It preserves Expenses as the source of truth and never repairs financial state with direct table edits.

## Common safety rules

- Confirm tenant, legal entity, expense/advance/card identifier, and current state before acting.
- Use the authenticated API or the owning repository command. Do not update or delete finalized expenses, payments, ledger facts, evidence history, or advance balances directly.
- Capture the failed outbox/inbox message id, correlation id, error code, and current version before retrying.
- Retry only idempotent commands and re-read persisted state after each attempt.

## Operational recovery

| Failure | Safe recovery | Stop condition |
|---|---|---|
| OCR failed | Inspect the OCR job and provider error; worker delivery retries transient provider failures with the same message id, then use the OCR retry endpoint after the provider is healthy, or use the manual completion endpoint with Finance-reviewed suggestions. | Finalized/reversed expense, unauthorized actor, or evidence no longer Ready. |
| Receipt inaccessible | Reissue a short-lived Platform Files download through the evidence endpoint; if the file is unavailable, attach a replacement through the upload-intent/completion flow and preserve history. | Never expose a permanent URL or replace the old evidence row.
| Duplicate suspected | Review duplicate candidates and keep one authoritative expense; reverse an already-finalized duplicate through the compensating reversal workflow. | Never delete a finalized fact.
| Policy misconfiguration | Retire the affected published policy version, publish a corrected effective-dated version, and re-evaluate only eligible drafts. | Never mutate a published rule snapshot.
| Policy exception | Request the exception against the persisted finding, route it through Platform Approvals, and record the authorized reason before finalization. | Never change the published rule or set a policy evaluation to Passed by SQL.
| Approval stuck | Inspect the Platform Approval request and resource version, then retry the request/decision with the authorized actor. | Never set `approval_state` directly.
| Accounting failed | Inspect the expense and Ledger fact/outbox status, then retry the owning semantic posting/recovery path. | Do not create a manual journal or mark Posted by SQL.
| Reimbursement failed/pending | Re-read the batch and Payments request; retry the idempotent payment command after Payments recovery. | Never mark an obligation Settled without a completed Payments result.
| Card feed duplicate | Replay the provider reference; terminal financial identity conflicts require a correction/refund/credit/dispute adjustment with a new provider reference. | Never overwrite amount/currency of a terminal card transaction.
| Card match wrong before finalization | Unmatch the draft card transaction, correct the draft expense, then match again. | Finalized/processed matches are immutable.
| Personal card classification correction | Use the personal-card command with the HR-scoped employee; settle the resulting employee repayment through Payments. | Never reassign a repayment to an employee outside the legal entity.
| Advance payment failed | Re-read the issuance reservation and Payments result; retry the idempotent issuance command. | Never start applications before a completed issuance.
| Advance application mismatch | Verify employee, currency, amount, and FX snapshot; correct only through an application/return/reimbursement command. | Never edit `applied_amount` or balance directly.
| Advance overdue | Run the overdue worklist and request approved settlement/write-off using the versioned workflow. | Write-off requires authorized approval and a Ledger fact.
| Employee repayment failed | Inspect the repayment obligation and Payments result; retry settlement after correcting the provider failure. | Wrong amount/currency or non-completed Payments results fail closed.
| FX rate missing | Configure/approve the FinanceBase rate for the transaction date and retry the operation. | Never invent an exchange rate in Expenses.
| Tax validation failed | Correct the Tax Engine configuration/evidence and retry evaluation. | Never calculate a statutory rate inside Expenses.
| Projection rebuild | Reprocess the relevant outbox/inbox message with the same message id and verify no duplicate financial rows. | Do not bypass Inbox idempotency.
| Database restore | Restore to an isolated database, run DbMigrator verification, seed checks, RLS checks, and financial reconciliation before promotion. | Never restore over an active production database without the approved change process.

## Verification after recovery

Record the final persisted state, history row, outbox/inbox status, Ledger fact/journal id, Payments id, and reconciliation difference. Expected difference is zero for finalized Expenses, reimbursements, employee repayments, card allocations, and advances.
