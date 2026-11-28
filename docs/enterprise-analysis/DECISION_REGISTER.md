# Decision register

| ID | Decision | Owner | Status | Evidence / next action |
|---|---|---|---|---|
| EA-001 | Manual explicit run only; no implicit Analysis runs. | Product Owner | `USER_LOCKED` | Product requirements and README contract. |
| EA-002 | Immutable historical result with provenance and filter snapshot. | Product Owner | `USER_LOCKED` | Persistence and UX contracts in this package. |
| EA-003 | Analysis is an L4 business capability, not an assumed L6 layer. | Engineering | `ENGINEERING_RECOMMENDED` | Current manifest layers and explicit host registration. |
| EA-004 | Analysis durable jobs use PostgreSQL; no new broker. | Engineering | `ENGINEERING_RECOMMENDED` | Existing PostgreSQL/RabbitMQ/Worker evidence. |
| EA-005 | Heavy computation runs in a separate minimal .NET process role. | Engineering / Operations | `ENGINEERING_RECOMMENDED`; `NEEDS_OWNER_APPROVAL` for deployment | Current Worker has mixed operational workloads. |
| EA-006 | Collections Hygiene is first pilot. | Product Owner | `NEEDS_OWNER_APPROVAL` | Ranked comparison and Subledger source evidence. |
| EA-007 | Subledger/owner capabilities are the only source path; no direct Analysis table reads. | Engineering | `USER_LOCKED` + `ENGINEERING_RECOMMENDED` | Module manifests and provider pattern. |
| EA-008 | Current-balance pilot does not promise historical knowledge-time reconstruction. | Engineering / Product | `ENGINEERING_RECOMMENDED` | Subledger pack requires immutable application history for historical aging. |
| EA-009 | No autonomous business command or “AI approval”. | Product Owner | `USER_LOCKED` | Recommendation lifecycle contract. |
| EA-010 | Commercial entitlement and quota policy. | Product Owner | `NEEDS_OWNER_APPROVAL` | Must reuse existing entitlement/activation; no parallel system. |

### Superseded statements

Any earlier statement that Planning lacks a runtime owner is `SUPERSEDED` by the current Planning closure register and repository state. The old P0 evidence is not rewritten; it remains an as-of record.
