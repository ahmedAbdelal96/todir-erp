# Module 28 Backend-to-Frontend Handoff

This is a backend handoff for the implemented planning foundation plus the metadata, submission, spreading, allocation and statement slices. Frontend implementation is not included in this mission.

## Runtime contract

Base route: `/api/v1/planning`. Requests require authenticated tenant context, the relevant `planning.*` permission and an active Planning module lifecycle. Cookie writes require the existing CSRF header. Tenant and legal-entity scope are enforced server-side.

Implemented route groups:

- cycles and lifecycle transitions;
- models, model versions and plan versions;
- sparse grid query/write-batch/spread/paste-preview;
- scenarios;
- assumptions and values;
- baselines, revisions, transfers and budget control;
- workforce, CapEx, revenue drivers, working capital and FX;
- actual refresh run status and variance query;
- calculation/recalculation and commentary.
- dimension members/mappings and constrained formula definitions;
- submission-unit state transitions;
- scenario share/adopt-preview and version comparison;
- exact spreading (`/grids/spread-exact`), weighted allocation, P&L query and forecast-accuracy calculation;
- workforce baseline import/cost-impact and CapEx lifecycle actions.
- Planning OpenAPI contract: `docs/api/planning-openapi.json` (63 paths; static validation is separate from business acceptance).
- Purchasing and Expenses request contracts may carry `budgetBucketId` as a validated selection hint. The backend remains authoritative: it derives the monthly OPEX control context, resolves the applicable active bucket, rejects cross-entity/context/uncontrolled selections, and fails closed for active strict policies with no applicable bucket. Owner transactions are rolled back for blocked/approval-required decisions.

## Development seed

The disposable/development seed provides, per tenant:

- one FY2027 monthly annual-budget cycle;
- one active integrated model and published model version;
- one published base plan version and shared Base scenario;
- Revenue input of 150,000 EGP for 2027-01;
- a published 150,000 EGP baseline and Warn control bucket;
- aggregate workforce planning of 12 FTE at 18,000 EGP average cost;
- one 45,000 EGP prioritized CapEx item;
- one 100 × 1,500 EGP revenue driver, working-capital assumptions and USD/EGP planning FX.

## Permission boundary

The administrator seed role receives the canonical Module 28 permissions. Other roles do not receive planning access by default. The frontend must treat permission results as visibility guidance only; the API remains authoritative and must handle 401/403/lifecycle errors without technical details.

## Not yet safe to build against as final product scope

Coordinate-scoped submission locking, delegated Platform Approvals, complete owner-module source adapters/events, full typed DAG execution, OPEX driver workflows, integrated Balance Sheet/Cash Flow reconciliation, rolling forecast jobs, commitment reconciliation, override SoD, Excel/CSV staging, multi-entity rollups, planning observability/runbooks and complete accuracy reporting remain backend gaps. The current Purchasing/Expenses reservation path is not a complete lifecycle guarantee: partial receipt/invoice/return/correction reconciliation and Expense reversal correction are still unverified. The P&L response is a planning projection, not a statutory statement; the cash-flow response is explicitly a proxy. Do not create UI claims for these capabilities until the backend matrix is updated with PASS evidence.
