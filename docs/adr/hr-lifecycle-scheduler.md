# HR lifecycle scheduling

## Decision

The Worker runs a clustered daily HR lifecycle job per tenant and legal entity. It processes four independent workflows in isolated repository transactions:

- apply employment termination records whose effective date has arrived;
- insert missing policy-driven monthly or anniversary accrual movements up to the run date;
- close annual leave balances at the January 1 boundary, carry eligible balances under the policy cap into a separately tracked expiring entitlement, and expire carryover lots whose expiry date has arrived;
- create idempotent reminder inbox records for due or upcoming HR milestones.

Accrual movements use the same period and policy-version idempotency key as the authorized HR accrual command. Missed monthly/annual periods remain eligible until created, and each transaction inserts at most 5,000 movements so later runs continue the backlog. The source employment/contract state is read as of each accrual period; a correction is appended as a ledger movement rather than changing history.

## Failure and retry behavior

Each workflow has its own tenant-scoped transaction and error boundary. A failure applying one termination does not roll back another transaction or prevent accrual and reminder processing for the same scope. Termination status/version conflicts remain unapplied for HR review. Accrual and reminder retries rely on unique idempotency keys and source/date uniqueness.

The job logs its tenant/legal-entity scope on failures and retries due work on the next daily run. It does not calculate payroll money, write another module's business tables, or turn notification delivery failures into HR transaction failures.

## Leave-year policy decisions

The Product Owner selected January 1 as the annual leave-year boundary. Each policy is evaluated as effective on December 31 of the closing year; the next January 1 creates the carryover entitlement. A carryover quantity is capped by the policy's configured limit. Any uncarried unused entitlement is closed with an immutable expiry movement at the boundary.

Consumption uses first-in, first-out by earliest expiry date; entitlements without an expiry follow expiring lots. Each consumption/reservation movement is split across the lots it consumes, with immutable allocation rows linking each movement to its source entitlement. Rejections and cancellations append exact allocation reversals. This preserves the explanation of which entitlement funded each leave request.

Every carryover is its own positive ledger movement and expires on the corresponding boundary plus the policy's configured number of calendar months (for example, January 1 plus six months is July 1). The daily scheduler expires the remaining amount effective on that expiry date. Balance reads exclude expired availability even if the Worker is delayed; a later run records the overdue expiry movement idempotently. Expiry reminders are durable in-app HR inbox records and do not alter leave or payroll facts. External delivery channels remain optional under the HR pack.

The lifecycle job catches up missed January boundaries, processes each workflow in a tenant/legal-entity transaction, and limits one run to 5,000 inserted movements per scope so subsequent runs can continue backlog. Unique source keys plus immutable year-boundary movements make retries safe.
