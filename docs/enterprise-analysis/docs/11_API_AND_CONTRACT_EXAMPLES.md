# 11 — Proposed HTTP API & Cross-Module Contract Examples

**Important:** These are **illustrative designs**, **not existing ToDir endpoints**. Final routing prefix, URL semantics, module-scoped permission codes, DTOs and OpenAPI schema must conform to current API conventions and be approved in ADR. Public-facing API resources should be named for their real task, not `IntelligenceAndAnalytics` or `AIAnything`.

## API rule summary

- GET is read-only: never computes, enqueues, refreshes fact projections or modifies historical outputs.
- Only `POST` on explicit start action can create a run. No cron/event/action-on-navigation auto creation.
- Analysis filters are typed and whitelisted, not arbitrary SQL or user-supplied programming expressions.
- Creation requires `Idempotency-Key`, authenticated actor, current authorized scope/definition, entitlement and quotas.
- Results returned by source owner record/currency/access scope, not uncontrolled raw database records.
- Lists are cursor-paginated; payload sizes and search/sort bounded.
- `202 Accepted` is **not** final analysis success. All status and result reads are explicit.

## Proposed resource contract (subject to current `/v1` prefix)

| Method & route (example) | Action | Permission |
|---|---|---|
| `GET /v1/analysis-definitions?module=sales` | Allowed analysis types, input form schema, UX labels | relevant analysis read or run, effective access |
| `POST /v1/analyses` | Request run, typed input and `Idempotency-Key` | relevant `.analysis.run` + data/source scope |
| `GET /v1/analyses?module=sales&cursor=...` | Paginated saved runs | relevant `.analysis.read`; results scoped |
| `GET /v1/analyses/{runId}` | Status + saved metadata; no auto compute | scoped read/own-read policy |
| `GET /v1/analyses/{runId}/results?cursor=...` | Published output/paginated detailed rows | scoped read + evidence classification |
| `GET /v1/analyses/{runId}/recommendations?cursor=...` | Saved recommendations only | relevant recommendation read |
| `POST /v1/analyses/{runId}/cancel` | Explicit cancel (not rerun) | run owner or manager with permission |
| `POST /v1/recommendations/{id}/reviews` | Human accepts as proposal/rejects/defers | relevant `.recommendation.review` |
| `GET /v1/recommendations/{id}/reviews` | Paginated review history | scoped read |
| `POST /v1/recommendations/{id}/action-request` | **FUTURE/disabled by default:** owner-workflow draft handoff | reviewer + owner module action permission |

Route placement: a single service may handle analysis runs across modules, but `analysisDefinition.module` is explicit. Frontend URLs remain business-task routes like `/sales/analysis`. If existing API framework favors module-owned controllers for scoped permissions, expose a thin domain path wrapper **only when needed**, never duplicate compute engines.

## Example: Create Collections Hygiene run

```http
POST /v1/analyses
Authorization: Bearer <scoped-session-token>
Idempotency-Key: 7dcf6c32-4d61-4c12-8e40-26753a6cbd55
Content-Type: application/json
```

```json
{
  "definitionCode": "collections.hygiene",
  "definitionVersion": 1,
  "legalEntityId": "00000000-0000-0000-0000-000000000101",
  "scope": {"branchIds": [], "customerIds": []},
  "filters": {
    "asOfDate": "2026-10-31",
    "activityFromDate": "2026-10-01",
    "activityToDate": "2026-10-31",
    "currencyCode": "EGP",
    "includeOpeningBalances": true
  }
}
```

**All UUIDs above are placeholders, not real company IDs.** An empty `branchIds` / `customerIds` must have an explicitly defined meaning; **do not assume “all”** unless effective access and analysis definition authorize it. `tenantId` is **not accepted** from the user payload.

Example only:

```http
HTTP/1.1 202 Accepted
Location: /v1/analyses/00000000-0000-0000-0000-000000000201
```

```json
{
  "runId": "00000000-0000-0000-0000-000000000201",
  "status": "Queued",
  "createdAtUtc": "2026-11-22T09:30:00Z",
  "resultAvailable": false,
  "statusUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201"
}
```

## Example: Completed run detail (sample fields, not fictitious business data)

```json
{
  "runId": "00000000-0000-0000-0000-000000000201",
  "definitionCode": "collections.hygiene",
  "definitionVersion": 1,
  "status": "Completed",
  "businessAsOf": "2026-10-31",
  "activityPeriod": {"from": "2026-10-01", "to": "2026-10-31"},
  "sourceKnownAtUtc": "2026-11-22T09:30:10Z",
  "publishedAtUtc": "2026-11-22T09:31:00Z",
  "resultVersion": 1,
  "quality": {"state": "Partial", "warnings": ["SOME_RECEIPTS_HAVE_UNKNOWN_BANK_CONFIRMATION"]},
  "resultsUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201/results",
  "recommendationsUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201/recommendations"
}
```

No invented figures. Actual amount metrics are retrieved in source-scoped result resources and must each specify currency, aggregation rule, evidence provenance, and coverage.

## Typed filter definitions

A versioned registry controls allowed definitions and filter schema:

```text
AnalysisDefinition {
  code: "collections.hygiene"
  moduleOwner: "subledger"
  filterSchemaVersion: 1
  businessLabelAr: "تحليل تحصيلات العملاء"
  scope: LegalEntity [plus optional allowed branch/customer]
  required: asOfDate
  optional: activityFromDate, activityToDate, currencyCode,
            customerIds, includeOpeningBalances
  rules: date horizon and customer-set size bounded by approved policies
  outputSchemaVersion: 1
  engineVersion: "collections-hygiene-v1"
  recommendationPolicyVersion: 1
}
```

The text above is a proposed contract catalog, **not a new database-driven arbitrary formula interpreter**. Version definitions via code or approved metadata, not tenant-uploaded scripts.

## Error contract (illustrative)

| HTTP | Domain issue | Behavior |
|---|---|---|
| 400 | Invalid date/unsupported filter/unknown type | No job created; machine error code and Arabic field message |
| 401/403 | Not logged in or unauthorized for action/scope | No job; no disclosive details |
| 404 | Run inaccessible or not found | Same concealment convention; no existence leak |
| 409 | Same idempotency key different payload / stale review version | No duplicate/new action; explicit conflict |
| 422 | Data readiness unsupported or contradictory period/metric | Controlled reason; no fabricated result |
| 429 | Quota/concurrency exceeded | Reason and retry policy; no queue storm |
| 503 | Analysis creation temporarily disabled | Operational ERP still available |

Exact use of 422 vs terminal `InsufficientData` must be chosen per definition and documented; don't mix inconsistent states.

## Cross-module internal capability example (conceptual C#, NOT paste-ready)

```csharp
public interface ICollectionsAnalysisFactsReadCapability
{
    Task<AnalysisFactPage<CollectionsFact>> ReadPageAsync(
        VerifiedScope scope, CollectionsFactQuery query,
        AnalysisSourceCutoff cutoff, string? cursor,
        int limit, CancellationToken cancellationToken);
}
```

- Define types in approved contracts project and map to owner-specific DTOs; avoid referencing `Modules.Subledger.Repositories` or internal entities from Analysis.
- `VerifiedScope` cannot be constructed from untrusted tenant string; backend obtains authorized actor context.
- Owner uses RLS, validates authorized source fields and returns `NextCursor`, `Watermark`, `KnownAt`, `Completeness`.
- No C# code emitted by an AI agent should assume this interface already exists.

## Functional contract tests

1. All GET endpoints are nonmutating and issue zero analysis jobs, even repeated.
2. POST double-click or lost response is idempotent; mismatched key/payload conflict.
3. Same run ID with unauthorized tenant/entity returns concealment status; no side-channel data.
4. Large result tables bounded; pagination stable when new runs created.
5. Historical run `definitionVersion=1` is interpreted with its own v1 contract after v2 introduced.
6. Unknown dimension or invalid date basis is rejected rather than silently ignored.
