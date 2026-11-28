# Product and pilot dossier

## Product decision

Analysis is a decision-support capability embedded in business work. It is not a global “AI Intelligence” product area and it is not a replacement for official operational reports. A user selects a definition, scope, date semantics and filters, then explicitly presses **بدء التحليل**. A page load, refresh, filter edit, event, schedule or GET request never creates a run.

Every published result states its facts, definitions, source versions, scope, cutoffs, warnings and limitations. Recommendations are explainable work items for human review. They never post accounting, settle an open item, alter inventory, change a sales stage, send a customer communication, or call an arbitrary business command.

## Ranked pilot comparison

| Rank | Candidate | Value hypothesis | Source readiness | Main boundary risk | Decision |
|---:|---|---|---|---|---|
| 1 | Collections Hygiene | Prioritize overdue/partially settled receivables and surface follow-up evidence without changing balances. | Strongest: Subledger owns open items, due dates, open base amounts, source reference/version and application history; current provider pattern proves scoped access. | Collections is a separate future owner; exact historical as-of needs source history, not only current balances. | `ENGINEERING_RECOMMENDED`; `NEEDS_OWNER_APPROVAL` |
| 2 | Sales Performance | Explain order/fulfilment/invoice conversion and stale pipeline signals. | Medium: Sales is an orchestrator and relevant facts span Sales, Invoicing, Inventory, Pricing, Tax and Subledger. | Risk of creating a second sales forecast or revenue truth; cross-owner completeness must be proven. | Conditional fallback |
| 3 | Inventory Health | Detect slow-moving, shortage and reservation pressure. | Medium: Inventory owns stock/reservation/valuation facts, but useful health requires UOM, valuation and time-series completeness. | Valuation and stock-movement semantics are high risk; no duplicate inventory report. | Deferred |
| 4 | Bounded CRM health | Surface stale leads/opportunities and activity gaps. | Medium-low for a first slice: CRM has stage history/forecast concepts, but activity and historical completeness need owner contract confirmation. | CRM forecast must not imply accounting revenue; potential overlap with CRM management screens. | Deferred |

No quantitative market or savings claim is made here. The value statements are hypotheses to validate with the Product Owner and domain users; external market validation is not evidence of repository readiness.

## Recommended pilot

**Collections Hygiene** is the recommended first pilot, subject to Product Owner approval and explicit acceptance by the Subledger/Collections domain owners. Business-facing name: **تحليل تحصيلات العملاء**. Technical analysis code: `collections-hygiene`.

The pilot uses deterministic, reviewable signals such as overdue open balance by approved bucket, partial settlement, concentration by party, and data-completeness warnings. It does not create collections cases, payment applications, write-offs, dunning notices or credit decisions. If the owner rejects the Collections boundary or the required approved read contract is not available, the conditional fallback is Sales Performance after a new source-contract review; no silent substitution is allowed.

## Explicitly deferred

LLM-generated claims, autonomous actions, Python, vector search, a data warehouse, a read replica, a global analytics shell, cross-tenant benchmarking, predictive credit scoring, daily persisted aging snapshots, and all non-pilot analyses are deferred.
