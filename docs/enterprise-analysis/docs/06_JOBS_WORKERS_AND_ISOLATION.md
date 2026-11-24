# 06 — Manual Jobs, Isolated Execution, Reliability and Fairness

**Decision:** User-created on-demand AnalysisRun only. Job workers may run continuously to **service accepted user jobs** but must never schedule/create analyses on their own. **Architecture:** PROPOSED-ADR.

## 1. Request vs process separation

```text
User clicks «بدء التحليل» → POST /v1/analyses (illustrative)
    → auth/permission/entity/entitlement validation
    → definition/filter/cost limits
    → transactional create run + job + idempotency marker
    → HTTP 202 Accepted {runId,status:Queued}
    → dedicated Analysis Worker claims durable job
    → owner-approved fact extraction in bounded chunks
    → deterministic analysis + (eligible) recommendations
    → validate evidence and data quality
    → atomically publish saved, immutable result + complete job
    → GET /v1/analyses/{id} returns saved result / job state only
```

**GET never queues, creates or recomputes.** SSE/poll for status is a read-only channel. No page mount, prefetch, user search, auth refresh, form change, scheduled cron, event handler, or fallback retry causes POST automatically.

## 2. Separate worker process

Operational `backend/Worker/Program.cs` currently hosts many background tasks, Quartz jobs and RabbitMQ consumers. Heavy analytical execution therefore belongs to an **independently deployed and independently selectable process role**, with explicit CPU/memory/network/DB-pool limits. First prove whether the existing Worker executable can run an analysis-only composition without registering operational consumers; do not add a second host/project by assumption. A second executable requires an explicit solution-boundary and deployment ADR. Distinct worker pool must not share operational process resource budget. The job store initially uses PostgreSQL scoped claiming. Need not create a RabbitMQ queue in the first iteration; Quartz, already present, **may** support polling/maintenance but is not automatically the entire job framework.

**Runtime isolation is not only separate .NET classes.** Separate host/container instances + cgroup limits, small connection pool, bounded `MaxDegreeOfParallelism`, throttled DB statement timeout, CPU limits, queue-age monitoring and deploy kill switch. If DB is shared, protection remains incomplete until mixed-load tests pass.

## 3. State and resilience contract

`Queued → Claimed/Running → Completed | Failed | Canceled`.

Additional attributes (not replace terminal state): `result_freshness=Current|Stale|Superseded`, `data_quality=Ready|Partial|Insufficient`. A completed job can have a stale result later **without rewriting its immutable output**. Failed jobs have sanitized operational metadata, not a fabricated report.

Claim requires `job_id`, `tenant_id`, `legal_entity_id`, `lease_owner`, `lease_until`, `attempt`, `deadline`, heartbeat and `claim_epoch` fencing token. Only current lease holder can publish. Crash leaves run queued/retryable after lease expiry; completed publication idempotent. Workers check cancellation and deadlines at chunk boundaries. An attempt may repeat computation; **at-least-once processing + effectively-once publication**, not impossible exactly-once execution.

## 4. Idempotency is distinct from computation reuse

- **Submission idempotency key:** bound to requesting principal + tenant/scope + body hash, with bounded key lifetime; identical retry returns same run, different body with same key → 409. Never create second job on network retry/double click.
- **Analytic fingerprint/cache key:** definition + version + normalized parameters + authorized tenant/entity/branch + source watermarks + model/config version + privacy scope. May suggest reuse of a previous completed output **only if contract/business semantics permit and user still authorized**.
- **Explicit new run:** clicking again may intentionally produce a new execution, even with the same visible filters, especially after source data changes. No automatic reuse that violates the user's expectation of a new dated result.

## 5. Claiming and tenant context

Worker must discover pending jobs across tenants without using `erp_app` as a broad RLS bypass. Evaluate a **narrow fixed-parameter security-definer dispatcher** (pattern already exists for platform outbox) or an approved privileged broker; it returns only minimal claim routing identity and is audited. Once tenant is known, all fact/result queries use **transaction-local** `app.tenant_id`, current authorized legal entity, and effective scope; no connection re-use leakage. No generic tenant ID supplied by untrusted payload overrides verified job ownership.

A persistent background job does **not** retain perpetual user privilege. Decide permission semantics at both `request time` and `data fetch/publish/action time`, and honor revoked access/cancellation in a defined way. At minimum, no stale revoked user can view results; high-risk processing should cancel or fail safe per approved policy.

## 6. Resource budgets and fairness

- Per-instance bounded parallel runs, statement and total execution deadlines, bounded memory and response size.
- Per-tenant and per-legal-entity concurrency quotas to avoid a large tenant starving others; weighted/fair scheduling or fair claim ordering when metrics justify it.
- No unbounded tenant-wide joins, memory materialization or export; cursor/keyset or batch extraction.
- Bulk data extraction may use short transactions/owner snapshots with explicit cutoff; avoid long-running transactions holding MVCC bloat and locks.
- Backpressure: if workload too high, return `Queued` with meaningful status; enforce hard queue/job limits and reject oversized inputs **before** creating expensive work.
- Read replica only when measured; if used, record replication lag and do not claim newer sources were analyzed.

## 7. Retry and error policy

| Case | Policy |
|---|---|
| Worker crash | Lease expires; retry from checkpoint with fenced publication |
| Temporary DB disconnect | Limited jittered retries, no busy-loop; separate operational metrics |
| Invalid filters/no authorization | Synchronous 400/403; no job creation |
| Missing source data | Publish explicit insufficient-data result or validation rejection, depending definition |
| Financial invariant mismatch | Fail-closed; preserve diagnostic trace and source IDs for operator, not user-visible raw SQL |
| Model bug / results fail sanity bounds | Fail job, do not show unvalidated recommendations |
| Duplicate request | Same run ID returned; no duplicate charge or compute |
| User cancels | Best-effort cooperative cancellation; no partial published output silently shown as complete |
| Source changed during extraction | Consistency check; retry bounded or publish controlled warning; never silently combine inconsistent cutoffs |
| Worker disabled | Existing runs readable, new creation disabled or queued according to configured policy; daily ERP unaffected |

## 8. Queue choice — exact decision gate

**Initially prefer Postgres job store + dedicated worker**, subject to measured contention. Existing RabbitMQ for operational integration events remains unchanged. Dedicated Analysis queue becomes justified if job wait-time, throughput, multi-language dispatch or operational isolation test fails. Do not abuse `OutboxPublishedMessage` fanout for heavy compute; domain events can still inform **future owner projections** without creating new analysis runs, under a separately approved change.

## 9. Load-test failure assertions

- Same bank/customer invoice/stock transaction API path works when Analysis service is off.
- When 2–N large jobs run, sales order confirmation, invoice posting, stock availability and payment settlement stay under **approved baseline budgets**, no growing DB lock queue or excessive outbox lag.
- Kill Analysis Worker during extract, compute and result commit → no missing/duplicate final result, no orphan `Running` job.
- Kill/disable feature + rerun → historical saved results remain available if access policy allows.
- Tenant A job cannot use source rows or persisted output of tenant B, including after pooled-connection reuse.
- Simulate 429 or quota exhaustion → clear domain response and zero unbounded backlog.

## 10. Deployment and rollback

- Deploy schema and contracts forward-compatibly; keep operational hosts unaltered until safe release.
- Feature flag/entitlement gate rollout of analysis routes and creation; disable new jobs without editing operational workflows.
- Rollback worker/app binaries without deleting historical runs. Forward-only remedial migration for schema problems, never destructive rollback on live financial datasets.
- Runbook covers drain, pause, resume, retry, dead-letter diagnostics, tenant job concurrency and stale evidence remediation.
