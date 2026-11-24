# 07 — Persistence, Historical Integrity, Lifecycle & Data Retention

**Status:** conceptual schema and invariants. **No SQL migration authorized yet.** Exact schema names, table shape and indexes require ADR approval and DB replay tests. Owner: new Analysis bounded context, **not** other business modules' canonical tables.

## Why not just save one JSON report?

We need queryable history, idempotency, exact run filters, reproducibility, reliable recommendations/decision status, tenant-scoped auth, pagination, audit and analytics quality metrics. A single opaque JSON blob fails lifecycle/query/versioning; excessive normalization of every chart cell adds overhead. Use **typed relational headers + carefully versioned JSONB for bounded domain-specific result payloads**, with separate controlled high-volume evidence as needed.

## Candidate normalized logical entities

| Entity | Purpose / critical fields | Write owner | Immutability |
|---|---|---|---|
| `analysis_definition` | stable code, module owner, display translation key, definition/version, input JSON schema, allowed dimensions, algorithm reference, max scope, state | Analysis admin/controlled deployments | Versioned definitions; never reinterpret an existing run |
| `analysis_run` | tenant/entity/optional branch, definition/version, request actor, submitted at, period/as-of, normalized filter hash, idempotency reference, job status, started/finished, consistency/cost metadata | Analysis | Mutable **operational state only** until terminal |
| `analysis_run_filter` (optional) | validated normalized business filters and UI rendering values | Analysis | Snapshot sealed once queued |
| `analysis_job_attempt` | attempt, lease, checkpoint, worker ID, error category, heartbeat, limits | Analysis worker | Append attempts; mutable lease with fencing |
| `analysis_result` | run ID, output schema version, metrics summaries, source watermarks, data quality, engine build, result artifact pointer/hash, published_at | Analysis | **Append/sealed on publish**; changes require new version/run |
| `analysis_evidence_ref` | run/result/metric/recommendation, source module/type/opaque ID, selected validated fields, snapshot hash, evidence access class | Analysis | Sealed; access checked at read time |
| `analysis_recommendation` | run ID, rule/model version, business issue, action option, support, expected impact/risk/uncertainty, evidence, expiry, current status | Analysis | Immutable proposal fields; review states separate |
| `recommendation_review` | recommendation ID, actor, decision (ReviewLater/AcceptedAsProposal/Rejected), reason, timestamp, concurrency version | Analysis | Append-only reviews; latest review projection derived |
| `recommendation_action_link` | optional later handoff, owner module, operation/draft ID, idempotency, status and actor | Analysis + owner-owned command contract | Append/controlled state; no autonomous execution |
| `recommendation_outcome` | measurable subsequent actual, source owner, evaluation period, result version, known-at, observed change and caveats | Analysis | Append evaluation versions; never rewrite original proposal |
| `analysis_audit_event` | who, what, when, scope, prior/new state, correlation and security event | Approved platform audit infrastructure | Append-only |

**Do not create all tables automatically.** Split actual schema only where usage, size, query patterns, ownership and audit requirements demand it; no EAV/unbounded flexible cube.

## Critical invariants

1. Composite tenant and legal entity keys; RLS FORCE on all tenant-owned tables, nonowner app role, no `BYPASSRLS`, exact access scope.
2. DB uniqueness for request idempotency and one final published result per run/engine version; owner transaction publishes final result and terminal job state atomically with fencing.
3. No float financial amounts; exact `numeric`/decimal and currency code + conversion source/rate date. Units always explicit.
4. Full run filter snapshot sealed once request accepted. Client changing later form fields does not affect queued job.
5. UTC instants for system events; separate business `date`/local calendar and legal entity timezone. Do not infer UTC timestamp cutoff as the same as fiscal period.
6. Historical output is **immutable even if source ERP data later corrected**; mark freshness separately, with linked new run. Changes to evidence confidentiality must preserve legal deletion/redaction policy without silent financial rewriting.
7. All collection/history/result list APIs use bounded pagination, stable sorting and filters. Large row-level result sets must have server pagination or artifact cursor, not giant JSON in GET.
8. PII and bank/employee data encrypted and access-controlled as required; never store payment tokens, credential secrets or open-ended raw transcripts.

## Run lifecycle vs recommendation lifecycle

**Analysis run:** `Queued → Running → Completed` / `Failed` / `Canceled`; `DataQuality: Ready / Partial / Insufficient` separate; `Freshness: Current / Stale / Superseded` separate projection. No `Superseded` job state.

**Recommendation:** `Proposed → Reviewed [AcceptedAsProposal | Rejected | NeedsMoreInformation]`; optional `HandoffRequested → DraftCreated` belongs to an **explicit later integration**, not the v1 auto-command. The review lifecycle records who and why; approval of analysis suggestion is not legal authorization to execute a payment or credit-limit adjustment.

## Historical report identity

```
RunIdentity = (tenant, legalEntity, optional branch/access policy,
               definitionCode, definitionVersion, normalizedParameters,
               sourceCutoffsAndWatermarks, engineVersion, requestedBy, submittedAt)
```

The user-facing report shows `الفترة التي تم تحليلها`, `البيانات متاحة حتى`, `التحليل أُنشئ في`, `الإصدار`, `المراجع`, `التحذيرات`. These are not redundant.

## Publication procedure (logical transaction)

- Verify lease/fencing token, run scope, permissions/entitlement policy, immutable filters, source watermark and result schema.
- Validate numerical invariants (totals, currencies, expected ranges), evidence sufficiency and no direct unauthorized data references.
- Insert results and approved recommendations as **one consistent unit**; atomically mark run `Completed` and record audit. Failed validation results in `Failed` or controlled `Insufficient`, never pseudo-success.
- Retries cannot append second official result or duplicate recommendation, even if compute repeats.

## Proposed index/query design to validate with EXPLAIN

- History: `(tenant_id, legal_entity_id, domain_code, requested_at DESC, id DESC)` plus active schema's RLS policy.
- Requested-by filter: `(tenant_id, legal_entity_id, requested_by, requested_at DESC, id DESC)` only if observed query patterns justify it.
- Jobs: partial index on `status IN ('Queued','Running')` + `available_at`, `lease_until`, `priority` with safe claim order and fair tenant scheduling; define uniqueness on scope/idempotency.
- Recommendations: `(tenant_id, legal_entity_id, run_id, review_status, id)`; don't add high-cardinality unneeded indexes until measured.
- Partition/history archiving when retention/size metrics cross measured thresholds; **not default**.

## Retention and deletion policy (must be agreed before production)

- Operational job attempt logs have short retention (proposal to configure by tier); published run records and decision audit may require longer retention subject to legal advice and commercial tiers.
- Deleting a run is **not automatically permitted** simply because a user wants to free storage; retention, legal holds and evidence must be defined with product/legal oversight.
- Avoid unbounded append-only accumulation; document archive/export/delete/replay with scope and audit.
- Keep read-model/temporary input chunks separately with shorter retention than published historical report; encrypt sensitive intermediate data, delete according to policy.
- Tenant offboarding/deactivation must respect contracted export and deletion, current entitlement/read-only history and audit obligations; do not assume one policy fits Egypt and Saudi Arabia.

## Schema migration approach

- Reversible application toggles do **not** imply destructive DB rollback. Follow current DbMigrator forward-only SQL conventions and seed/migration harness.
- New Analysis tables/schemas and RLS deployed first with no operational change; integration code behind feature gates; worker last; UI last.
- Tests: clean DB replay, incremental existing DB upgrade, RLS with `erp_app` and dispatcher scopes, concurrency/idempotency, fresh/old run reads, backups/restores, audit/retention.

## Deferred design decisions

`Exact schema name`, `Object Storage provider`, `Retention defaults`, `Whether to persist per-result tables or JSONB`, `Encryption keys`, `Result anonymization policies`, `Legal requirements by Country Pack` — **TO-VERIFY/ADR**. Never invent compliance retention requirements.
