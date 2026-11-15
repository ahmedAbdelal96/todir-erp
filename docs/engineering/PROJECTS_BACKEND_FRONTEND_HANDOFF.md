# Projects Backend → Frontend Handoff

Canonical pack: `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Backend scope is complete for the Projects module and its accepted Sales source, source-owned cost projections, billing, settlement, capital handoff, change order, close exception, and platform notification seams. Delivery status: `MODULE 23 BACKEND COMPLETE + DEVELOPMENT SEED READY — FRONTEND HANDOFF`.

## Primary APIs

- `POST /api/v1/projects` — create a project.
- `POST /api/v1/projects/from-sales-source` — create one Customer Project from an accepted Sales quotation revision. Request: `legalEntityId`, `quotationId`, `acceptedRevision`, `name`, optional `projectManagerEmployeeId`, dates, `billingModel`, optional `initialBudget`, `description`, and required `idempotencyKey`.
- `GET /api/v1/projects` and `GET /api/v1/projects/{projectId}` — bounded list/detail reads.
- `POST /api/v1/projects/{projectId}/tasks`, `/team`, `/milestones`, `/time`, `/budgets`, `/forecasts`, `/billing-rules`, `/charges`, `/invoice-requests`, `/changes`, `/capital-handoffs`, and `/close-exceptions` — explicit business actions.
- `POST /api/v1/projects/changes/{changeRequestId}/decide` — approve or reject a change request. Approval creates the current contract/budget/schedule revision while preserving prior versions.
- `POST /api/v1/projects/close-exceptions/{exceptionId}/resolve` — resolve a close blocker.
- Read endpoints for time, budgets, forecasts, costs, commitments, billing, invoice requests, capital handoffs, baselines, documents, timeline, portfolio, resource load, and close readiness are paginated or bounded where applicable.

## Core response concepts

`ProjectDetail` exposes `sourceType` and `sourceRef` for stable commercial traceability. Financial reads keep these concepts separate: `Budget`, `ActualCost`, `OpenCommitment`, `Etc`, `Eac`, `BillingEligible`, `Invoiced`, `Collected`, and `Outstanding`. Do not derive one from another in the client.

Project lifecycle states are `Draft`, `Active`, `OnHold`, `ExecutionCompleted`, `FinancialClosing`, `Closed`, and `Cancelled`. Time, budget, billing, change, capital, and close actions return domain errors when the lifecycle or ownership invariant does not permit the action.

## Permissions and isolation

Backend authorization is authoritative. Use the module permissions `projects.read`, `projects.create`, `projects.edit`, `projects.manage_team`, `projects.manage_tasks`, `projects.time.read`, `projects.time.create`, `projects.time.approve`, `projects.budget.read`, `projects.budget.manage`, `projects.forecast.read`, `projects.forecast.manage`, `projects.cost.read`, `projects.cost.manage`, `projects.billing.read`, `projects.billing.manage`, `projects.billing.writeoff`, `projects.change.manage`, `projects.close.manage`, `projects.capital.manage`, `projects.audit.read`, and `projects.export`.

Every request is tenant-scoped and legal-entity scoped where the route includes an entity. Cross-tenant, wrong-entity, stale-revision, duplicate-source, closed-project, and invalid-transition attempts are rejected by the backend. Salary and payroll data are not returned by Projects; labor cost facts use approved Project time and rate snapshots.

## Integration contracts

Projects consumes typed source events for Expenses, Purchasing, Invoicing, Inventory, Payments/Subledger, and Fixed Assets. Source modules remain authoritative. Delivery is idempotent through Inbox and `project_integration_event`; duplicate, delayed, missing-currency, missing-amount, invalid-source, and out-of-order events are recorded rather than guessed.

Sales exposes `ISalesQuotationCapabilities.GetAcceptedQuotationAsync`. Projects accepts only the current accepted revision and stores the stable source reference `QuotationId:Revision:N`; later quotation revisions require the approved Change Order workflow.

The local runtime path was verified with a real finalized Expenses outbox message delivered through RabbitMQ to `projects-source-integration`, processed by the Worker, committed through the Projects Inbox and PostgreSQL projection, then replayed without a duplicate cost fact or integration event.

## Frontend readiness notes

- Keep list pages read-only; creation/editing is a dialog or dedicated route.
- Surface actionable domain errors by code, never raw SQL or exception text.
- Treat `409`/concurrency errors as refresh-and-retry flows, not silent overwrites.
- Show source provenance and revision on project detail, but do not expose tenant, schema, or implementation identifiers as user fields.
- Arabic copy must use the governed Projects vocabulary and remain RTL-native.

## Evidence

- `ProjectsTests`: 16/16 persisted acceptance scenarios.
- `ProjectsCrossModuleAcceptanceTests`: 9/9 real PostgreSQL owning-module slices, including the local Outbox → RabbitMQ → Worker → Inbox → PostgreSQL path.
- `BrokerIntegrationTests`: 2/2 against local RabbitMQ.
- Prior valid regressions: Invoicing 17/17, Payments 23/23, architecture 21/21, Release build, formatter, and OpenAPI contract.
- DbMigrator applied/verified 206 scripts; Development Seed completed for Atlas and Delta.
- PJ38 heavyweight load certification is explicitly deferred. Frontend/browser acceptance is a separate mission.
