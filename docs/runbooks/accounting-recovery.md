# Accounting recovery

This runbook restores an Accounting operation through its owning module and the
canonical Ledger/Subledger contracts. It is for an operations engineer with the
required tenant, legal-entity, and permission scope.

## First response

1. Record tenant, legal entity, correlation ID, source reference, business date,
   and the user-visible error code.
2. Read the Accounting health, failed-operation, reconciliation, close, and
   outbox views. Preserve the original journal, source document, snapshots,
   audit entries, and outbox rows.
3. Retry only with the original idempotency key after the underlying cause is
   corrected. A retry must converge to the existing result, not create a second
   financial fact.
4. If a posted journal or completed stock movement exists, correct it with the
   owning module's reversal or adjustment workflow in an open period.

## Recovery cases

| Symptom | Safe recovery |
| --- | --- |
| Missing account mapping | Open the Ledger readiness issue, create or activate the versioned mapping, then retry the source operation. Never post to a suspense account to bypass the blocker. |
| Failed financial operation | Inspect the source lifecycle, Accounting fact, journal, audit, and outbox state. Retry the same idempotent operation only after confirming the durable state. |
| AR/AP reconciliation difference | Run the owning Subledger reconciliation and Accounting reconciliation for the same entity/date. Repair the source workflow or use the documented corrective command, then rerun reconciliation. |
| Inventory reconciliation difference | Run Inventory's balance rebuild and Ledger reconciliation for the same scope. Resolve the stock movement or valuation failure through Inventory; do not edit Ledger balances. |
| Balance projection mismatch | Run the Ledger projection reconciliation/rebuild command against a maintenance database connection. Record the result and verify canonical journal totals afterward. |
| Period accidentally closed | Stop new posting, identify the close run and approval, and use the authorized period-reopen action. Record reason, actor, and correlation. Do not alter posted entries. |
| Approved reopen | Reopen only with the dedicated permission and explicit business reason. Re-run close readiness before closing again. |
| Opening import failure | Keep the batch in its current lifecycle state, inspect row-level validation errors, correct the import source, and retry the same batch operation. Never delete posted opening entries. |
| Opening reconciliation difference | Compare the immutable opening batch, Ledger balance, and AR/AP/Inventory controls. Correct the source batch or post an approved adjustment in an open period, then rerun go-live checks. |
| FX revaluation issue | Preserve the preview and rate snapshot. If the run is `Invalidated` after any lines posted, use the explicit next-period reversal to reverse only committed lines; uncommitted lines remain unposted. A fully posted run uses the normal reversal workflow. |
| Outbox backlog | Follow [outbox-backlog.md](outbox-backlog.md), restore broker/worker health, and verify pending rows become published with no duplicate facts or journals. |
| Restore/DR validation | Follow [database-restore.md](database-restore.md), run migrations and development-independent health checks, reconcile Ledger/Subledger/Inventory, and verify tenant/entity isolation before service resumption. |

## Forbidden recovery actions

Operations must not directly update or delete `journal_entry_line`,
`account_period_balance`, `customer_balance`, `supplier_balance`, posted source
documents, or completed stock movements. Do not rewrite exchange-rate snapshots,
posted journal history, or audit history. If a row appears inconsistent, stop the
workflow, capture evidence, and use the owning module's rebuild or reversal
contract.

## Exit evidence

Record the before/after lifecycle states, exact retry key, created/reversed fact
identifiers, reconciliation result, outbox status, audit entry, and the actor
who approved any reopen or corrective action. A recovery is complete only when
the source state, Ledger, Subledger/Inventory projection, and outbox are all
reconciled.
