# P0-08 — P1 Decision Brief

P1 should not start implementation. It should close these decisions in order:

1. **Capability boundary:** platform-owned capability or formal Analysis module; choose schema, manifest layer, lifecycle, entitlement and ownership.
2. **Execution boundary:** existing Worker role with hard limits or separate host; define queue, retry, cancellation, deployment and health semantics.
3. **Canonical read contracts:** owner-module contracts for the selected pilot; no direct table reads and no second source of truth.
4. **Temporal truth:** valid-time, knowledge-time, corrections, late-arriving facts, snapshot versioning and reproducibility.
5. **Security proof:** tenant, legal entity, branch, record ownership, entitlement expiry, RLS and cached-result isolation.
6. **Performance proof:** rerun the disposable baseline, set measured SLOs, and prove no core ERP regression under representative Analysis load.
7. **Pilot selection:** approve Collections only if its data-truth gates pass; otherwise select a bounded candidate and record the reason.

## P1 exit evidence

P1 should return an approved ADR set, a signed read-contract matrix, a disposable runtime/security test report, a measured performance baseline, and a pilot-specific Definition of Ready. Until then, P2 remains prohibited.

