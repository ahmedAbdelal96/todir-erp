# P0-B06 — Performance Acceptance Recommendation

**Status:** proposed guardrail set; P0 local baseline measured, production guardrails not approved.

Future Analysis acceptance should require evidence for:

- per-operation and per-tenant p50/p95/p99 with explicit minimum samples;
- operational ERP regression compared with the same seed, mode, machine class and concurrency;
- successful and failed request counts with bounded error rates;
- PostgreSQL connection/pool budgets, transaction duration, lock waits and query timeout behavior;
- API, PostgreSQL and worker CPU/memory/I/O observations from the same run;
- queue/outbox backlog and retry/dead-letter behavior when a Worker is in scope;
- tenant fairness under unequal tenant sizes;
- cancellation, abort and cleanup behavior;
- repeatability across independent warm and cold runs.

The provisional `5% p95` statement is not approved by this P0-B run. The canonical disposable migration/seed gate and local operational baseline now pass, but the Development Seed and single-host measurements are insufficient to lock a production or Analysis budget. A benchmark run before an Analysis Worker exists cannot prove mixed Analysis/ERP isolation.

