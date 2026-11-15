# Projects Backend Execution State

## Mission

Authorized scope: canonical Module 23 backend/database, cross-module contracts, development seed, and backend acceptance evidence. Frontend/browser acceptance is excluded by the mission brief.

## Current checkpoint

- Status: NOT COMPLETE
- Canonical pack: `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Completed: repository discovery, architecture reconciliation, design and implementation plan; Projects module boundary, isolated schema/RLS, repository/service/API, seed, shared source/capitalization contracts, rate-card snapshots, milestones, dependency-cycle checks, budget revisions, project-scope authorization, typed Expenses/Purchasing/Inventory project allocation fields and payloads; source-event Inbox idempotency with persisted Unallocated/Applied/Ignored evidence; immutable cost-fact corrections/reversals; commitment amendment/cancellation projection; partial invoice allocation with retry conflict protection and release on rejection/cancellation; Subledger-backed collected/outstanding composition; Fixed Assets capitalization-candidate handoff and accepted/rejected result events; strict source-event currency/amount/source-id validation and out-of-order handling; server-side bounded pagination; contract-value/forecast-based profitability summary; invoice-finalization and correction projection; forward migrations for invoice source ownership and invoice corrections; persisted PJ40-PJ47 PostgreSQL acceptance tests, including a real MassTransit consumer path.
- Unfinished: the full repository gate is not reconciled because PJ38-heavy Subledger certification is explicitly excluded and two unrelated Subledger latency tests still fail under the configured isolated database (`AgingLatencySeparatesCurrentHistoricalAndNormalPartyWork`, `AutoApplySuggestionLatencyStaysBoundedForLargeParties`); the broad IntegrationTests run did not complete within the bounded local run. The GitHub Actions API lookup returned HTTP 404 for the configured remote, so current remote check status is unavailable rather than assumed green.
- Last meaningful change: fixed `ProjectExistsAsync` source-event tenant context and multi-command PostgreSQL handling, added deterministic PJ47 MassTransit/PostgreSQL acceptance for valid, duplicate, missing-currency, and out-of-order events, synchronized the code manifest with the invoice-correction table and rejection event, rebuilt, and reran the isolated Projects suite.
- Next executable action: resolve or explicitly defer the unrelated Subledger latency failures and complete the bounded integration/security gate on the isolated database; then rerun the final full gate after the last change.
- Evidence still valid: isolated `ProjectsTests` pass (14/14), `InvoicingTests` pass (17/17), `ArchitectureTests` pass (21/21), full `Backend.sln` Release build passes with 0 warnings/errors, and `git diff --check` passes. The isolated database `todir_invoice_p03` is migrated and seeded with the configured runtime/admin roles; PJ47 proves valid source cost persistence, duplicate inbox suppression, missing-currency evidence, and out-of-order evidence. Earlier repository-wide execution without env variables produced 96 immediate configuration failures; with the isolated variables, Accounting (8/8), Inventory (12/12), Ledger (55/55), and Security (7/7) passed, while Subledger had 39/41 passed and the two named latency failures.

## Coverage rule

Track each MVP capability through requirement → business rule → persistence → API → seed → automated test → runtime evidence. Do not report backend completion until the affected rows have current evidence after the last source change.
