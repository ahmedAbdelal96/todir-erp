# Projects Backend Design

## Intent

Implement the backend/database foundation for canonical Module 23 so a tenant can create Customer, Internal, and Capital projects; execute work; capture and approve project time; derive cost facts; control budget/forecast/billing facts; manage change and close; and hand capitalizable cost to Fixed Assets through a typed contract. Projects remains the owner of project facts and never duplicates customer, employee, expense, purchasing, invoice, inventory, ledger, payment, or fixed-asset truth.

## Goals

- One tenant-safe `business_projects` schema with database-enforced lifecycle, immutability, scoped uniqueness, RLS, bounded indexes, and correction history.
- REST contracts under `/api/v1/projects` using existing authorization, transaction, audit, outbox, numbering, and module-manifest patterns.
- Real development seed for Atlas and Delta with distinct project types, lifecycle states, execution records, time, budget, forecast, cost, billing, and close cases.
- Focused domain, database, API, isolation, idempotency, and reconciliation tests for the canonical PJ40–PJ46 behaviors that are applicable to the backend mission.

## Non-goals / Deferred

Frontend/browser acceptance, PJ38 heavyweight load certification, Primavera-grade scheduling, EVM, intercompany projects, retention/holdback, customer portal, AI planning, and a new invoice or asset subsystem remain outside this backend implementation unless an existing public contract is required.

## Constraints

- Work directly on `dev`; use the existing modular-monolith structure and PostgreSQL bootstrap migration model.
- No cross-module private table/DbContext access. Cross-module facts are stable references, public capability calls, or events.
- Budget, commitment, actual, ETC/EAC, billing eligibility, invoice, and collection remain separate facts.
- Actual cost is source-derived and corrective; salary is never copied into Projects or exposed to project managers.
- Every tenant-owned table is RLS-enabled and forced; runtime access uses the existing tenant transaction context.

## Approaches Considered

1. Add a broad Projects module as one implementation project with focused domain entities, repository SQL, service orchestration, one schema baseline, explicit public contracts, and seed stages. Recommended: it follows the current repository pattern, preserves module ownership, and keeps the implementation reviewable while covering the complete documented fact model.
2. Build each PJ stage as a separate .NET project/subsystem. Rejected: it violates the repository's one implementation project per significant module rule and would create unnecessary dependency and transaction boundaries.
3. Add only API/read-model stubs and defer persistence/integrations. Rejected: it cannot prove financial truth, RLS, idempotency, or development seed behavior and would violate the Module Pack's backend mission.

## Design Decisions

- The module uses a single `Projects` implementation project and a small `Shared.Contracts.Projects` contract surface only when cross-module handoffs need to be public.
- Project numbers are allocated with the platform transactional sequence allocator (`projects.project-number`, `PRJ-`), never `MAX+1`.
- The repository owns transaction-scoped SQL and read projections; the service owns workflow validation and action policy; endpoints own transport/authentication only.
- Project actuals are represented by immutable `project_cost_fact` rows with source identity and correction links. Commitments and billing charges have their own tables and idempotency keys.
- Fixed Assets integration is a typed capability boundary; no asset/CIP tables are written by Projects.

## Acceptance Basis

The implementation is evaluated against the canonical Module 23 pack, especially simple project creation/execution, time approval without Attendance mutation, privacy-safe rates, commitment-before-actual, source-derived cost, budget/forecast correctness, fixed-price/milestone/T&M billing, invoice request/allocation boundaries, profitability drilldown, change-order history, execution-versus-financial close, capital handoff, isolation/redaction, idempotent retries, and cross-module reconciliation.

## Open Gaps to Resolve in Code

- Existing upstream modules expose different levels of public project-dimension contracts. The Projects schema stores typed source references and the module exposes capability contracts; any missing upstream producer is recorded as an integration exception rather than replaced with private table access.
- The current repository has no Projects implementation or seed stage, so all module composition, migration, seed, and focused test surfaces are new.
