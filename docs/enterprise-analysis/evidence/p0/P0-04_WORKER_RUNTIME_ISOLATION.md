# P0-04 — Worker Runtime and Isolation

**Result:** `BOUNDARY UNRESOLVED`

## Current Worker facts

`backend/Worker/Program.cs` composes 20 existing MassTransit consumers, Quartz scheduling, and hosted services in one executable. The current registrations include:

- consumers for Catalog, Payments, Banking, HR, Payroll, CRM, Expenses, Projects, Manufacturing, Quality and cache invalidation;
- scheduled jobs for HR expiry reminders and Expenses reminders;
- hosted services for Outbox dispatch, FinanceBase rate sync, Pricing activation, Inventory reservation expiry and Service maintenance.

`backend/Worker/Worker.csproj` references Core plus the current operational modules. There is no Analysis Worker project or process registration.

## Risk assessment

Adding Analysis work to this executable without an isolation decision would couple analytical load, memory, database connections and queue behavior to ERP operations. It would also blur ownership of retries, deployments, health probes, entitlements and observability.

## P1 decision required

Choose and document one of:

1. **Existing Worker extension:** Analysis execution is a bounded role within the Worker, with explicit queue/consumer limits, database pool limits, cancellation, retry/dead-letter policy, and starvation tests.
2. **Separate host:** A formal host/process is introduced with project-graph, deployment, health, entitlement, schema, and outbox/inbox decisions.

Neither option is authorized by current source alone. The user-locked on-demand behavior means no scheduler may create Analysis Runs implicitly.

