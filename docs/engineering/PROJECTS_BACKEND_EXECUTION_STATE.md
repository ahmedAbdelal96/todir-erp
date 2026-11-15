# Projects Backend Execution State

## Mission

Authorized scope: canonical Module 23 backend/database, cross-module contracts, development seed, and backend acceptance evidence. Frontend/browser acceptance is excluded by the mission brief.

## Current checkpoint

- Status: IN PROGRESS
- Canonical pack: `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Completed: repository discovery, architecture reconciliation, design and implementation plan; Projects module boundary, isolated schema/RLS, repository/service/API, seed, shared source/capitalization contracts, rate-card snapshots, milestones, dependency-cycle checks, budget revisions, project-scope authorization, typed Expenses/Purchasing/Inventory project allocation fields and payloads; source-event Inbox idempotency with persisted Unallocated/Applied/Ignored evidence; immutable cost-fact corrections/reversals; commitment amendment/cancellation projection; partial invoice allocation with retry conflict protection and release on rejection/cancellation; Subledger-backed collected/outstanding projection; Fixed Assets capitalization-candidate handoff and accepted/rejected result events; PJ40-PJ46 contract/domain acceptance tests.
- Unfinished: Invoice/AP correction and Payments/Subledger events remain evidence-only because their authoritative allocations are owned by Projects/Invoicing/Subledger; persisted PJ40-PJ46 acceptance workflows and final git delivery.
- Last meaningful change: added source-owned allocation fields, reversal/correction projection, capital handoff consumer, scenario tests, and executed the canonical migration/seed against Docker PostgreSQL.
- Next executable action: run the final full backend gates and inspect fresh runtime evidence, then commit and push the authorized backend changes.
- Evidence still valid: `ProjectsTests` pass (13/13), Worker build passes with 0 warnings/errors, Docker PostgreSQL migration and Development Seed complete successfully for database `todir`, `business_projects` rows are tenant-isolated under RLS (no tenant context returns 0; tenant context returns 4), and Worker starts with both Projects integration endpoints connected to RabbitMQ. Full solution/architecture/diff-check must be rerun after the latest changes.

## Coverage rule

Track each MVP capability through requirement → business rule → persistence → API → seed → automated test → runtime evidence. Do not report backend completion until the affected rows have current evidence after the last source change.
