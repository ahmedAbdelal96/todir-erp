# Fixed Assets recovery runbook

This runbook is for semantic recovery through the Fixed Assets API and approved module capabilities. It applies to one tenant and legal entity at a time. Preserve the original record, use a new idempotency key for a correction, and record the business reason and evidence reference.

## Recovery paths

| Failure | Recovery command | Required proof |
| --- | --- | --- |
| Capitalization source mismatch | Put the candidate back in review, correct the source allocation through the allocation correction command, then re-approve | Source snapshot and corrected allocation total |
| Duplicate candidate | Retry with the same source type/reference; a matching result is idempotent, a different payload is a conflict | Immutable source reference |
| Inventory serial conflict | Resolve the serial in Inventory, then create a new candidate from the authoritative Inventory snapshot | Product, serial, movement and valuation snapshot |
| Missing book setup | Configure the Asset Book and account mappings before retrying capitalization or depreciation | Legal entity, currency, book and mapping readiness |
| Depreciation calculation exception | Leave the run prepared/calculated, correct the policy or source data, and prepare a new keyed run | Calculation basis and exception reason |
| Depreciation posting failure | Retry the same run; Ledger fact idempotency prevents duplicate journals | Run status and Ledger fact reference |
| Ledger outage | Keep the business operation pending, restore Ledger capability, and retry the same idempotency key | Outbox/ledger status and reconciliation result |
| Incorrect assignment | Create a dated return/transfer assignment and handover; never edit history | Effective dates, actor and acknowledgement evidence |
| Verification mismatch | Record the observed result and resolve the campaign exception through the verification workflow | Scan, notes and resolution actor |
| Asset not found | Confirm tenant, legal entity, asset number/tag and permission scope before retrying | No cross-tenant or cross-entity lookup |
| Wrong disposal | Use the approved reversal/reinstatement workflow when available; otherwise stop and escalate to the accounting owner | Original disposal fact and approval decision |
| Reinstatement | Create a new authorized lifecycle event linked to the original disposal; do not delete the disposal | Original disposal and new approval |
| CIP allocation mismatch | Correct the source allocation or add a reversing CIP source, then re-run the ready-for-use gate | Remaining source amount and CIP cost-source ledger |
| Opening import mismatch | Reject the invalid lines, correct the import batch, validate, and post once | Legacy reference, balance equation and GL reconciliation |
| Projection rebuild | Run the approved DbMigrator projection rebuild for the tenant/entity after the source tables are healthy | Rebuild count and before/after reconciliation |
| DR restore validation | Restore to a disposable environment, run migrations and seed verification, then check RLS, immutable tables and Asset/GL reconciliation | Restore log and verification report |

## Forbidden recovery

Never use direct SQL as a normal recovery:

```text
UPDATE fixed_asset SET employee_id = ...
UPDATE asset_book SET book_value = ...
DELETE asset_book_transaction ...
UPDATE accumulated_depreciation = ...
DELETE disposed_asset ...
```

The database is the system of record, but financial and custody corrections must remain semantic, auditable and idempotent.

## Performance directive

`FA37 LARGE-SCALE PERFORMANCE HARDENING — NOT EXECUTED BY PRODUCT OWNER DIRECTIVE`.

Normal delivery still requires bounded queries and appropriate indexes. The 1M-asset, 100k-line, 100k-verification and burst-load certification suite must not be executed as part of ordinary product delivery.
