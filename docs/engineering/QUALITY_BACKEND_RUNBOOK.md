# Module 27 Quality Backend Runbook

This runbook is for operational recovery of Quality records. PostgreSQL remains the system of record, Inventory remains the owner of stock status and physical availability, and every repair must go through an authenticated Quality or owner-module workflow with an audit reason.

## Recovery rules

- Stop the affected workflow, preserve the original record and capture tenant, legal entity, document, correlation and idempotency values.
- Never delete or rewrite a completed inspection, result, restriction, disposition, certificate version, NCR or CAPA history.
- Do not change a stock balance or status with ad-hoc SQL. Reconcile through the typed Inventory owner capability and replay the original idempotency key where applicable.
- Partial releases are durable operations. The same restriction and idempotency key must keep the same quantity, UOM, dates and reason fingerprint; a changed payload is a conflict. Competing requests may be rejected while another owner operation is in flight.
- Scrap, supplier return and rework are owner-module operations. Quality records the decision and owner operation, while Inventory, Purchasing or Manufacturing remains authoritative for the side effect.
- After recovery, verify the outbox/inbox state, owner operation, Quality state history, audit event and the user-visible read API.

## Common incidents

| Incident | Recovery action |
|---|---|
| Wrong plan or specification | Supersede the plan version, correct applicability, and create a reinspection. Existing plan snapshots remain historical. |
| Duplicate inspection | Keep the first inspection as the source of truth, mark the duplicate through the supported correction workflow, and reconcile its source fingerprint. |
| Incorrect result | Do not edit a completed result in place. Record a correction with reason/evidence and run reinspection. |
| Instrument out of calibration | Block completion, attach the calibration evidence, repeat the measurement with a valid instrument, and preserve the original failed/blocked result. |
| Pending or unknown Inventory hold | Inspect `quality_restriction_release_operation`, the owner idempotency key and Inbox/outbox state. Retry the same typed Inventory operation; never mark the Quality hold released locally. |
| Unknown disposition | Keep the disposition non-Executed until the owner operation is confirmed. Reconcile Inventory, Purchasing return or Manufacturing rework evidence and the approval audit before retrying. |
| Wrong Use-as-Is decision | Cancel the unexecuted disposition where allowed, obtain a new approval with concession evidence, and release only through Inventory. |
| Duplicate NCR | Use the source reference and tenant-scoped audit trail to link the records; do not delete the duplicate. |
| Wrong scope or tenant | Stop processing, revoke the request, and re-run authorization with the correct legal entity. Never repair by changing tenant identifiers. |
| CAPA reopened | Record a new effectiveness review and keep the prior closure evidence. Reopen only through the CAPA lifecycle workflow. |
| SCAR stuck | Confirm supplier scope, due date and response state, then record the next supplier action; do not bypass the SCAR state machine. |
| Wrong COA/CoC | Supersede the certificate with a new version from the frozen inspection snapshot and record the reason. |
| Projection or event lag | Check the RabbitMQ endpoint, platform Inbox, Quality integration event and source Outbox in that order; replay the source message idempotently. |
| Disaster recovery | Restore to a disposable database first, run migrations, validate RLS and tenant counts, replay outbox/inbox safely, then perform owner-operation reconciliation. |

## Forbidden SQL surgery

Do not delete Quality rows, set `inspection.decision_state`, set a restriction to `Released`, set a disposition to `Executed`, or alter Inventory balances directly in SQL. SQL is limited to read-only diagnosis and approved migration scripts under `backend/DbMigrator/Database/bootstrap/`.

## Local verification

Use a disposable PostgreSQL database for destructive acceptance. Run the DbMigrator, Development Seed, focused Quality tests, owner-module integration tests, Worker integration and the Release build. Preserve the development database and Docker volumes.
