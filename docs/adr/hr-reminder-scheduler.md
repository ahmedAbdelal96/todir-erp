# HR reminder scheduler scope

The Worker runs one daily Quartz job and asks `business_hr.list_hr_reminder_scopes()` for only tenant and legal-entity identifiers that have potentially remindable HR records. The function is owned by the approved `erp_dispatcher` role because a clustered background job has no interactive tenant context and the HR tables force tenant RLS. It returns no employee, employment, document, contract, or compensation data. The Worker then processes each scope through the normal tenant-scoped HR repository transaction.

Reminder rows are immutable, tenant- and legal-entity-scoped in-app inbox records. A unique source/type/date key makes retries idempotent. The reminder scan only reads HR-owned source records and never updates employment, payroll, leave, document, or access state. Delivery is best-effort; a later daily run retries after a scan failure.

The same daily job also invokes independent HR lifecycle operations for due scheduled terminations and policy accrual periods. Each operation owns its own tenant-scoped transaction and error boundary; a failure in one does not suppress the other or the reminder scan. See `docs/adr/hr-lifecycle-scheduler.md` for those lifecycle rules.
