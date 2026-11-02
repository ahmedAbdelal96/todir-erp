# HR performance verification scope

Status: Accepted Product Owner decision — 2026-11-02.

## Decision

Do not run or create large synthetic HR load campaigns during the current local
development mission. Continue with focused correctness tests, ordinary seeded
development data, bounded PostgreSQL query-plan inspection, pagination checks,
and review of indexes for known hot paths. Do not add a search cluster, cache,
or other infrastructure without measured evidence from an approved workload.

## Context

The HR Module Pack describes large-scale load scenarios as part of HR35. The
Product Owner explicitly stopped the current large synthetic data run and
rejected that testing approach for this mission because it is taking too long
and is preventing completion of the remaining work.

## Consequences

- No large synthetic employee, attendance, timeline, or import dataset is part
  of this mission's acceptance evidence.
- HR35 large-scale certification is deferred; no claim is made about behavior
  at those volumes.
- Correctness, tenant isolation, bounded queries, pagination, and migration
  reconciliation remain subject to focused local verification.
- Reopen large-scale certification only when the Product Owner requests a
  dedicated performance-hardening mission with an agreed environment and
  completion target.
