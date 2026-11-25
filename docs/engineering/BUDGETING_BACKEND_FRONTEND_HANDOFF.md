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
- Planning OpenAPI contract: `docs/api/planning-openapi.json` (67 paths; static validation is separate from business acceptance).
- Purchasing and Expenses request contracts may carry `budgetBucketId` as a validated selection hint. The backend remains authoritative: it normalizes the annual, quarterly, or monthly control horizon, resolves the applicable active bucket, rejects cross-entity/context/uncontrolled selections, and fails closed for active strict policies with no applicable bucket. Owner transactions are rolled back for blocked/approval-required decisions.
- Budget Control now normalizes annual, quarterly and monthly horizons, requires a published plan version, preserves source currency, and supports `POST /budget/overrides`, `GET /budget/overrides/{id}`, and `POST /budget/overrides/{id}/decision`. An override is a single source/document/line/version exception, is Platform-Approval governed, expires when configured, and is consumed transactionally; it never changes the approved Budget.
- Purchasing now exposes `/legal-entities/{entityId}/purchase-requisitions` create/read/submit/approve/reject/convert routes. Conversion is source-versioned and idempotent; the backend preserves PR line identity and replaces remaining precommitment with PO commitment.
- The isolated Development runtime acceptance procedure uses `ASPNETCORE_ENVIRONMENT=Development` and `DOTNET_ENVIRONMENT=Development` for the approved development OpenIddict signing material, a disposable migrated/seeded PostgreSQL database, the runtime `erp_app` role, and `TODIR_COOKIE_SECURE_POLICY=None` for local HTTP. Do not commit any of these credentials or signing artifacts. Release API output must be used for acceptance (`dotnet run --configuration Release --no-build`); Debug output can omit newly added routes.
- `POST /budget-control/reconcile` loads bounded owner facts from Purchasing and Expenses, compares Actual/Commitment/Precommitment by legal entity, period, metric and currency, persists a source watermark and discrepancy, and optionally applies a bounded projection rebuild. Strict `Block` control requires a successful zero-discrepancy owner result as well as the existing opening flag. This is still a backend contract, not frontend product acceptance.

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

Coordinate-scoped submission locking, configurable delegated approvals, Ledger YTD source provider, full typed DAG execution, OPEX driver workflows, integrated Balance Sheet/Cash Flow reconciliation, rolling forecast jobs, complete owner opening activation/recovery, full PO/receipt/return/final-invoice event replay, Expense failure-injection recovery, Excel/CSV staging, multi-entity rollups, planning observability and complete accuracy reporting remain backend gaps. The current Purchasing/Expenses reservation and recovery seams are not a complete lifecycle guarantee. The P&L response is a planning projection, not a statutory statement; the cash-flow response is explicitly a proxy. Do not create UI claims for these capabilities until the backend matrix is updated with PASS evidence.
