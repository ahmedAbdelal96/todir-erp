# POS Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver the backend-owned Retail POS capability through PostgreSQL, APIs, worker recovery, cross-module seams, security, seed, and reproducible acceptance evidence without implementing frontend screens.

**Architecture:** Add one `business_pos` module following existing `Modules.*`/`DbMigrator` patterns. POS owns retail context, carts, checkout orchestration state, tenders, returns, cash sessions, offline envelopes, and read models; Catalog, Pricing, Tax, Inventory, Payments, Invoicing, Ledger, and Directory remain authoritative for their facts. Internal cash paths use the transaction coordinator; external payment/fiscal operations use durable idempotent states and worker retry.

**Tech Stack:** .NET 10, ASP.NET Minimal APIs, Npgsql, PostgreSQL RLS, MassTransit/RabbitMQ, existing Core transaction/authorization/audit/outbox infrastructure, xUnit integration tests.

**Spec:** `docs/modules/20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

## Global Constraints

- Work on `dev`; do not modify frontend code.
- `business_pos` is the only POS schema and every tenant-owned table has enabled and forced RLS.
- POS never owns Catalog price/tax, inventory balance, payment completion, invoice lifecycle, or ledger truth.
- Completed sale history is immutable; corrections are new typed events.
- All money uses decimal PostgreSQL numeric precision and all scalable reads are bounded and indexed.
- Seed is deterministic, rerunnable, Development/Test only, and never runs from API or Worker startup.

## Review Focus

- Duplicate checkout/tender/replay identities must converge to one economic effect; test same-key and same-key-different-payload behavior.
- Cross-tenant, entity, branch, register, and revoked-terminal access must be denied by authorization and forced RLS.
- External payment success followed by internal failure must remain recoverable without a second charge.
- Concurrent stock/serial/return/session-close operations must reject stale or over-limit work without negative quantities.
- Failed migrations or seed reruns must be safe and preserve existing data.

### Task 1: POS module registration and schema

Create the module project, manifest, baseline schema, migration discovery, API/Worker composition, RLS policies, constraints, indexes, and execution-state checkpoint.

### Task 2: Operational foundation

Implement registers, terminals, sessions, typed cash movements, reconciliation, carts, optimistic versioning, hold/resume/cancel, and focused tests.

### Task 3: Authoritative retail calculation and checkout

Implement explicit Catalog/Pricing/Tax/Inventory/Payments/Invoicing/Ledger capability seams, durable checkout state, cash checkout, split tender, change, and idempotent internal completion.

### Task 4: Recovery, returns, refunds, exchanges, controls, and offline server contracts

Implement retry/recovery worker path, returns/refunds/exchanges, serial/lot/weighted validation, customer/credit and manager override boundaries, signed offline packages, sync envelopes/conflicts, and audit.

### Task 5: Operational reports, seed, performance and acceptance evidence

Add bounded read models, POS development seed, PostgreSQL/security/concurrency/integration tests, performance evidence, runbooks, PS00–PS41 matrix, final gates, one commit, and push.
