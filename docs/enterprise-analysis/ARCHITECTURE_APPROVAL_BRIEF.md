# Architecture approval brief

## Recommended decisions

| Decision | Recommendation | Status |
|---|---|---|
| Product boundary | Formal `analysis` L4 business module owning analysis lifecycle and results; business pages remain in the owner module. | `ENGINEERING_RECOMMENDED` |
| Schema | One Analysis-owned schema, proposed name `business_analysis`; no application data in `public`; RLS follows existing tenant/entity/branch rules. | `ENGINEERING_RECOMMENDED` |
| Host | New minimal .NET Analysis worker process in P2, using existing PostgreSQL/RabbitMQ/observability conventions; do not add heavy work to the operational Worker. | `ENGINEERING_RECOMMENDED`; deployment acceptance `NEEDS_OWNER_APPROVAL` |
| Job authority | PostgreSQL durable job state is authoritative. RabbitMQ may wake or signal work but is not the source of job state. | `ENGINEERING_RECOMMENDED` |
| Pilot | Collections Hygiene over approved Subledger read capabilities. | `NEEDS_OWNER_APPROVAL` |
| Commercial packaging | Reuse existing entitlement/activation and permission projection. Whether Analysis is included or a paid add-on, and any quota, is a Product Owner decision. | `NEEDS_OWNER_APPROVAL` |
| Action handoff | No autonomous command. A separately authorized, revalidated handoff may be added only after the owning workflow contract exists. | `USER_LOCKED`; handoff deferred |

## Decisions requested from Product Owner

1. Approve Collections Hygiene as the first pilot, or explicitly select the documented Sales Performance fallback.
2. Approve the Analysis capability’s commercial treatment: included entitlement, optional add-on, and whether quota/retention limits are customer-visible.
3. Accept a separate Analysis worker deployment unit with independent resource limits and operational ownership.
4. Confirm the business owner for the Collections Hygiene result/review page and the review SLA/operating policy. This is not a technical permission decision.

## Rejected alternatives

- A global AI/Intelligence navigation area: violates the user-locked business-context rule.
- A second truth in Planning, Banking, CRM, Ledger, Inventory or Payments: violates module ownership.
- Heavy jobs inside the existing operational Worker: no independent pool, failure or deployment isolation.
- Direct cross-module SQL, arbitrary exports or unrestricted table scans: violates ownership, security and performance boundaries.
- Python/vector/DWH/Kafka/new gateway in P1: no proven need or approved ADR.
- Automatic runs on read, refresh, event or schedule: explicitly rejected by product requirements.
