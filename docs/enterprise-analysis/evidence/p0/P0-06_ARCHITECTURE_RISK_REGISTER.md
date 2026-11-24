# P0-06 — Architecture Risk Register

| ID | Risk | Evidence | Severity | Required disposition |
|---|---|---|---|---|
| R-01 | Analysis module/host boundary is not current runtime reality | No Analysis manifest/API/host; current Worker is composed | Critical | P1 ADR: platform capability vs formal module; host/deployment decision |
| R-02 | Collections portfolio facts are not proven by party-level contracts | Subledger exposes party-scoped reads; no certified portfolio export found | Critical | P1 read contract and data-truth proof |
| R-03 | Temporal reproducibility is underspecified for late/corrected ERP facts | Contracts expose as-of fields in some reads, but knowledge-time evidence is not established | High | Define valid-time/knowledge-time and correction policy |
| R-04 | Performance baseline is absent | Docker preflight denied; no numbers produced | Critical | Re-run disposable baseline with owner-approved environment |
| R-05 | Existing Worker is a shared operational process | Single Worker registers consumers, jobs and hosted services | High | Isolation limits, queue policy, load/starvation tests |
| R-06 | Runtime security/isolation proof is absent | Static RLS/authorization evidence only | Critical | Disposable tenant/legal-entity/RLS acceptance suite |
| R-07 | Official forecasts could be contaminated by derived analysis | CRM and Banking own forecast/reconciliation surfaces | High | Keep Analysis outputs non-authoritative; execute only owner workflows |
| R-08 | Frontend has no Analysis destination contract yet | Registry contains current module IDs only | Medium | Add destination only after manifest/permission decision |

No risk is closed by compilation or by the existence of a design document.

