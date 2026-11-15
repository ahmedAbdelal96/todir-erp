# Projects Backend Execution State

## Mission

Authorized scope: canonical Module 23 backend/database, cross-module contracts, development seed, and backend acceptance evidence. Frontend/browser acceptance is excluded by the mission brief.

## Current checkpoint

- Status: NOT COMPLETE
- Canonical pack: `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Completed: repository discovery, architecture reconciliation, design and implementation plan; Projects module boundary, isolated schema/RLS, repository/service/API, seed, shared source/capitalization contracts, rate-card snapshots, milestones, dependency-cycle checks, budget revisions, project-scope authorization, typed Expenses/Purchasing/Inventory project allocation fields and payloads; source-event Inbox idempotency with persisted Unallocated/Applied/Ignored evidence; immutable cost-fact corrections/reversals; commitment amendment/cancellation projection; partial invoice allocation with retry conflict protection and release on rejection/cancellation; Subledger-backed collected/outstanding projection; Fixed Assets capitalization-candidate handoff and accepted/rejected result events; PJ40-PJ46 contract/domain acceptance tests.
- Unfinished: Invoice/AP correction and Payments/Subledger events remain evidence-only because their authoritative allocations are owned by Projects/Invoicing/Subledger; persisted PJ40-PJ46 end-to-end event acceptance is not automated; the repository-wide integration/security gate still requires its dedicated disposable-test connection environment variables.
- Last meaningful change: fixed Fixed Assets accepted/rejected event payloads to carry the originating project and handoff identifiers, rebuilt, started the Worker against RabbitMQ, and pushed commit `e5d8d01` to `origin/dev`.
- Next executable action: provide/configure the repository's disposable integration-test connection variables, then run the Projects persisted acceptance and repository-wide security/integration gates without using the seeded development database for destructive tests.
- Evidence still valid: `ProjectsTests` pass (13/13), full solution build passes with 0 warnings/errors, `ArchitectureTests` pass (21/21), `git diff --check` passes, Docker PostgreSQL migration and Development Seed complete successfully for database `todir`, `business_projects` rows are tenant-isolated under RLS (no tenant context returns 0; tenant context returns 4), and Worker starts with both Projects integration endpoints connected to RabbitMQ. The repository-wide `dotnet test backend/Backend.sln --no-build` is not a product failure: 96 tests fail immediately because `TODIR_P04_ADMIN_CONNECTION`, `TODIR_LEDGER_TEST_CONNECTION`, `TODIR_DATABASE_CONNECTION`, `TODIR_RABBITMQ_HOST`, and module-specific disposable test connections are unset.

## Coverage rule

Track each MVP capability through requirement → business rule → persistence → API → seed → automated test → runtime evidence. Do not report backend completion until the affected rows have current evidence after the last source change.
