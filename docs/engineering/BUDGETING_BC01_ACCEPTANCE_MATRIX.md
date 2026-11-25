# Module 28 BC01 Acceptance Matrix

This is the final focused evidence ledger for the BC01 closure mission. `PASS` means the required source workflow and financial outcome were actually exercised. A build or a Planning-only row is not financial acceptance.

| Scenario | Source workflow / required outcome | Evidence | Status |
|---|---|---|---|
| BC-T01 | Published budget and policy resolution | Planning focused tests; disposable PostgreSQL | PASS |
| BC-T02 | Annual/quarterly/monthly horizon and currency scope | Planning focused tests | PASS |
| BC-T03 | Partial reservation and idempotent retry | `PlanningBudgetControlIntegrationTests`; 7/7 focused suite | PASS |
| BC-T04 | Source-bound override and SoD | Planning focused suite and Platform Approval contract | PARTIAL — authenticated approval journey open |
| BC-T05 | Purchasing PR 100,000 → 40/60 PO conversion | New Purchasing PR repository/routes and migration | PARTIAL — real command vector not yet automated |
| BC-T06 | PO receipt/invoice/return/credit lifecycle | Existing Purchasing/Invoicing code paths | FAIL — complete source-to-Planning vector not proven |
| BC-T07 | Expense 30,000 reversal restores budget exactly once | Durable reversal-intent implementation; Expenses build | PARTIAL — injected failure and retry runtime proof open |
| BC-T08 | Owner opening reconciliation Actual/Commitment/Precommitment | Owner contracts, Purchasing and Ledger providers, durable owner result | PARTIAL — full Ledger YTD/source vector and mapping evidence remain open |
| BC-T09 | Strict activation after owner reconciliation | `PlanningBudgetControlCapability` guard and migration | PARTIAL — HTTP activation proof and watermark invalidation open |
| BC-T10 | Cross-module Outbox/Inbox replay and duplicate delivery | Existing platform infrastructure only | FAIL — BC01 real replay evidence not completed |
| BC-T11 | Runtime-role authenticated Planning/Purchasing/Expenses/Invoicing HTTP | Prior API composition 2/2 with admin connection | FAIL — documented `erp_app` password mismatch blocks runtime-role evidence |
| BC-T12 | Migration, seed, RLS, architecture, Release build | Disposable `bc01_pr_20261125`, 273 scripts; build; architecture 23/23 | PASS |

## Additional mandatory coverage

Policy resolution, source currency, negative-budget/shadow behavior, and reservation/correction idempotency have focused evidence. Project/CapEx control, multi-bucket rollups, concurrent amendment/consumption, revision/transfer contention, complete rollback, Ledger replay, and runtime-role security remain unverified for BC01. No PASS claim is made for those rows.

## Delivery decision

`BC01 NOT COMPLETE`. The implementation closes owner seams and strict-control fail-closed behavior, but the mandatory real Purchasing financial vector, canonical Ledger reconciliation/replay, runtime-role HTTP acceptance, and complete BC01 matrix still require engineering work. This document does not certify Module 28 or production readiness.
