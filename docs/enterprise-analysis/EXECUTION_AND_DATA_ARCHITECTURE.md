# Execution, persistence, recommendations and security architecture

## Proposed Analysis-owned records

P2 may add these records under `business_analysis`: `analysis_definition`, `analysis_definition_version`, `analysis_run`, `analysis_run_filter`, `analysis_job`, `analysis_job_attempt`, `analysis_result`, `analysis_result_section`, `analysis_evidence_reference`, `analysis_recommendation`, `analysis_recommendation_review`, `analysis_recommendation_outcome`, and append-only audit/provenance records. Names are design identifiers, not current tables.

Every tenant-scoped record carries tenant and legal-entity scope where applicable; branch scope is explicit where the owner contract supports it. RLS is enabled and forced using current platform patterns. Unique constraints include definition/version/scope/filter fingerprint and source/result identity as needed for idempotency. Results are immutable after publication; operational run/job status, freshness, lease and retry counters are mutable records kept separate from result content.

## Job lifecycle

`requested → queued → running → publishing → published` and terminal `cancelled`, `failed`, `expired`. Attempts carry lease id, worker id, started/heartbeat/completed timestamps, retry number, error code and source watermark. Claim uses a transaction with bounded lease and fencing token; stale claims are recoverable. Retry policy distinguishes transient source/transport errors from deterministic validation/authorization/data errors. Cancellation is cooperative, persisted and checked between bounded source pages and algorithm stages.

Processing is at-least-once. Publication is effectively-once through an immutable result key, idempotent publication transaction and fencing; the design does not claim exactly-once computation. Per-tenant concurrency, global concurrency, source page size, job timeout and memory limits are explicit configuration with kill switch and backpressure. One bad tenant/run cannot starve others.

## Recommendation lifecycle

`source fact → metric → signal → recommendation → human review → owner action (optional) → outcome`. Deterministic rules are the P2 baseline. A recommendation contains the triggering facts/evidence references, explanation, options, supportable impact, risks, limits, freshness and rule/definition version. Human review is not accounting approval and does not mutate the owner record. Any future action handoff revalidates current permissions, scope, record version and business invariants through the owner workflow.

## Security and entitlement contract

Proposed permission vocabulary: `analysis.catalog.read`, `analysis.run.create`, `analysis.run.read_own`, `analysis.history.read`, `analysis.result.read`, `analysis.evidence.read`, `analysis.recommendation.read`, `analysis.recommendation.review`, `analysis.run.cancel`, and `analysis.action.request`. These are permission descriptors for P2 review, not registered permissions today. A user who can create a run must receive an explicit own-run/result policy; create must not imply unrestricted tenant history.

Effective access is evaluated with the selected tenant/legal entity/branch, role membership, record policies, module lifecycle and entitlement. Tenant switch, scope change, revocation or activation change invalidates cached access and stale result views. Worker access is a service identity constrained to the run’s stored scope and source capability; it never bypasses RLS or impersonates a broader tenant. Evidence endpoints authorize the referenced source scope again.

No separate AI ACL, customer database, quota engine or subscription system is permitted. Commercial treatment and customer-visible quota remain `NEEDS_OWNER_APPROVAL`.
