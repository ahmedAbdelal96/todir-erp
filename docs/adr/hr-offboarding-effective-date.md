# Effective-dated HR offboarding

## Decision

Offboarding may be recorded before its effective end date. In that case the employment keeps its current status and records the future end date. The offboarding case reports `terminationStatus=Scheduled`; the record becomes `Effective` only after the scheduled date is reached.

A clustered daily Worker job applies due terminations in a tenant and legal-entity scoped transaction. It changes the employment status, ends or cancels contracts by their start date, appends the employment timeline event, writes audit evidence, and marks the offboarding transition applied. Row locks, status checks, and a one-way state transition make retries safe. Task completion remains a separate offboarding workflow and may finish before or after the employment end date.

IAM deactivation continues through the existing outbox and IAM capability. Its effective date is evaluated independently from the employment end date, so access may be revoked on the last working day while the employment remains active through its legal end date. The IAM job rechecks other active employments before revoking membership access.

Payroll receives final-exit facts only after HR has applied the termination and only for periods that include the effective end date. Scheduling a termination does not prematurely create final-exit inputs.

## Rationale

Writing `Terminated` at scheduling time would expose a future legal fact as current, while delaying creation of the offboarding plan would lose preparation time for asset return, handover, insurance, and payroll tasks. Separating task status from effective termination preserves both workflows and the historical record.

## Operational behavior

- The daily HR lifecycle job processes due cases after missed runs on its next execution.
- A failed case is rolled back and retried on a later run; it is logged with its legal-entity scope.
- A status/version conflict remains unapplied for HR review instead of silently overwriting a later employment change.
- Do not change `termination_status`, employment status, or contract state with direct SQL.
