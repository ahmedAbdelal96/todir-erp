# Platform Performance and Scalability Coverage Matrix

Date: 2026-11-22  
Branch: `dev`  
Scope: local performance foundation; Redis and higher-scale certification remain deferred.

| Requirement | Persistence/runtime | Backend/frontend delivery | Seed | Automated/runtime proof | Result |
|---|---|---|---|---|---|
| Request authorization reuse and fail-closed isolation | PostgreSQL authoritative; request-only subject cache | `NpgsqlAuthorizationSubjectStore` plus bounded metrics | Atlas + Delta memberships | Core/security suites from prior checkpoint; final benchmark uses both tenant sessions; missing/forbidden paths remain fail-closed | PASS |
| Representative ERP read paths | API → PostgreSQL, bounded pages | Benchmark covers Authorization, POS, Inventory, Sales, Invoicing, Payments, Accounting, HR, Payroll, Projects, Subledger | 240 scripts; Atlas/Delta realistic seed | Final broad local run: 5 requests at concurrency 1/4 per tenant/path; all authorized reads succeeded; p50/p95/p99/RPS recorded | PASS — local envelope |
| Representative ERP write paths | API transaction/outbox/DB persistence | Final diagnostic write smoke | Disposable Atlas seed | Projects, Sales quotation, Invoicing draft, Inventory warehouse, POS cart, HR employee, Payroll component, Payments account all HTTP 201 | PASS — diagnostic writes |
| SQL/pool/transaction/lock evidence | `pg_stat_activity`, `pg_stat_database`, `EXPLAIN (ANALYZE, BUFFERS)` plus request-scoped Npgsql Activity counting | No unproven query/index rewrite | Isolated DB only | Final diagnostic measured SQL statements/request for all 21 reads (6–50), critical Sales/Inventory/Projects/Subledger plans, connection/active/transaction/lock samples; `pg_stat_statements` is unavailable but is not required by the harness | PASS — local envelope |
| Worker durable delivery | Outbox → RabbitMQ → Worker → Inbox → project cost facts | Existing MassTransit consumers and Outbox/Inbox preserved | Valid `ExpenseFinalizedV1` payloads | Final local run: 10 messages, pending 0, 20 published including replay, 10 Inbox processed, 10 cost facts; 7.92 s / 1.26 events/s; duplicate replay unchanged | PASS — diagnostic throughput |
| Multi-tenant and two API instances | Same disposable DB, Atlas + Delta, two API processes | No cross-tenant state in session or URL | Atlas/Delta seed | Final harness starts two local API instances and records mixed Projects reads separately; no production scaling claim | PASS — local two-instance smoke |
| Browser operational responsiveness | Real Vite + API + Chromium | `performance-certification.spec.ts` | Development DB read-only runtime | 1 Playwright test passed across 8 operational routes; DOM/load/resource/bytes/long-task metrics logged | PASS — local browser evidence |
| Frontend payload reduction | Vite route-level chunks | Named dynamic imports; no route permission changes | All module route graphs | Typecheck, architecture, production build passed in prior checkpoint; browser gate confirms routes render | PASS |
| Migration and Development Seed | Canonical DbMigrator | No migration added by performance mission | 240 ordered scripts; Atlas/Delta seed | Fresh disposable migration/seed passed for each benchmark; DBs dropped | PASS |
| Security/RLS/correctness | Tenant RLS and immutable facts | Existing security tests retained | Isolated DB | Final isolated Security/RLS 7/7 and Projects acceptance 22/22; worker duplicate proof fresh | PASS |
| Repeatable regression budgets | JSON artifact under `artifacts/performance` | diagnostic/load/stress modes; p95/error/lock/duplicate budgets documented | Same seed and pool | Harness records commit, environment, timings, resources and limits | PASS — local regression gate |
| Manufacturing localization defect | Frontend test source | Preserved concurrent Manufacturing files | N/A | Existing false-positive remains | FAIL — pre-existing, recorded |
| Inventory OpenAPI defect | Frozen contract vs route | No performance-owned contract change | N/A | `api:drift` passes; contract gate still reports missing counts path | FAIL — pre-existing, recorded |

## Evidence interpretation

The results certify only the reproducible local development envelope described above. They do not certify production throughput, autoscaling, multi-region behavior, PJ38-scale data, or hardware outside the recorded environment. SQL statements/request are measured by the opt-in request-scoped Npgsql Activity counter; `pg_stat_statements` remains unavailable in the local PostgreSQL image and is not used as a substitute for the per-request measurement.
