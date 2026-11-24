# 17 — Mandatory Specification Template Before Implementing Any Analysis

**Purpose:** prevent every domain agent from inventing filters, formulas, output labels, recommendation rules or SQL independently. The master catalog is **not** sufficient to start implementing a new analysis: complete and approve one instance of this template per committed type.

```yaml
spec_id: "AN-[MODULE]-[NUMBER]"
status: "Draft|BusinessApproved|ArchitectureApproved|ReadyForImplementation|Accepted"
version: 1
owner_module: "e.g. subledger"
analysis_code: "stable code, e.g. collections.hygiene"
label_ar: "Arabic business wording"
label_en: "Natural English UI wording"
intended_user_roles: []
primary_decision_question: "specific managerial question"
measurable_business_outcome: "how value will be tested"
supported_legal_entities: "per effective access"
supported_branch_scope: "supported/unsupported + why"
source_owner_contract_versions: []
output_schema_version: 1
engine_version: "release/version"
```

## A. Business scope and definitions

- Problem statement in Egyptian business practice (not general AI marketing).
- Buyer/decision-maker and who can act on a recommendation; data sensitivity.
- Business purpose, what the result **does not claim**, and explicit difference from existing reports/forecasts/approval workflows.
- Success/failure examples and situations where **no recommendation** is correct.

## B. Filters and period semantics

| Field | Type | Required? | Default? | Legal choices and dependencies | Permission scope / error |
|---|---|---|---|---|---|
| Legal Entity | UUID resolved from trusted session | yes | current context only if allowed | permitted list | 403 if cross-scope |
| `fromDate` | date | type-dependent | none, unless approved | in valid fiscal/calendar range | 400 if invalid |
| `toDate` | date | type-dependent | none | >= from; max span | 400 if invalid |
| `asOfDate` | date | when snapshot analysis | none | distinct from activity period | 400 if invalid |
| Branch/record | scoped ID list | type-dependent | NOT silently all | bounded, authorized | 403/422 |
| Currency | ISO code | metric dependent | no silent mixing | explicitly supported source | 422 if unsupported |
| Comparison | enum | optional | none | precise basis defined | 400 if invalid |

Special: date boundaries inclusive/exclusive? fiscal timezone? source valid-time and known-at? Backdated transactions? identity of filters when user switches company? Sort/pagination? Maximum result size? Spell out all; **never guess**.

## C. Data source and ownership matrix

For each metric and field, document: `canonical owner module`, `existing method/contract + exact path`, `additional owner read capability required?`, `source version`, `status/transaction finality condition`, `currency/UOM policy`, `historical correction behavior`, `security scope`, `max cardinality`, `index/read-model plan`, `data readiness tests`. Any field not verified remains `BLOCKED`.

## D. Metric calculation truth table

For every metric:

```text
metric_code / Arabic label / description
population (which rows qualify), excluded states (draft/void/reversed)
period definition / as-of / known-at
formula with exact decimal rounding and currency/unit handling
separate denominators and missing/zero handling
independent reference calculation / source reconciliation
edge-case fixtures with expected values
completeness and sensitivity warnings
```

The formula must be *owner-approved*; no duplicated Accounting/Inventory calculation inside Analysis.

## E. Recommendation eligibility specification

For each `rule_code`:

- Objective condition and severity policy, threshold source/version.
- Minimum evidence and data quality required; which source faults suppress it.
- Human-readable recommended **review action**, not final autonomous command.
- Expected impact metric if estimable, assumptions, downside and alternatives.
- Owner operational action (if supported), permission and current-state preconditions.
- Expiry/staleness handling; output of the rule if data partially missing.
- Rule-specific positive, false-positive, no-action and regression fixtures.

## F. UX states

Include Arabic real user label and helper copy; valid empty state, queued/running/fail/partial/complete, saved history, tables and charts, evidence drilldown, accessible keyboard/mobility, English parity, URL/read cache rules and no auto-run assertion. Product owner/SME must approve screenshots or interactive prototypes **before full frontend build**.

## G. Security, privacy, entitlement

Permission matrix (read, own-read, run, review, action), legal entity and branch, source-record access, role changes after request, sensitive field redaction, retention/export/consent, back-end and DB negative tests. Never let a generic Analysis permission overrule source module rights.

## H. Operations and load

Representative data size, expected fact extraction cost, owner read query plan, max allowed timeframe, streaming/pagination, worker memory/CPU/pool/timeout, cancellation, peak period, replica lag policy. Tests: operational baseline and mixed-load p95/p99, DB lock, outbox metrics; no absolute SLA invented.

## I. Acceptance criteria and traceability

- Given/When/Then business examples including partial/reversal/negative facts.
- Exact fixture input, expected KPI and justification, human review.
- API/OpenAPI contract, job/retry/idempotency/historical immutability, source watermark.
- User clicks «بدء التحليل» and only then POST occurs; no analysis POST on GET.
- Completion requires real runtime/DB evidence and approval, no fake PASS.

## J. Release and maintenance

Owners, dependency version, historical migration/replay behavior, change-log, backward-compatible formula version, feature disable/recovery, monitoring, documentation updates and acceptance signoff. If calculation changes later, **new engine/definition version**; no silent reinterpretation of old runs.
