# Module 28 Backend-to-Frontend Handoff

This is a backend handoff for the implemented planning foundation only. Frontend implementation is not included in this mission.

## Runtime contract

Base route: `/api/v1/planning`. Requests require authenticated tenant context, the relevant `planning.*` permission and an active Planning module lifecycle. Cookie writes require the existing CSRF header. Tenant and legal-entity scope are enforced server-side.

Implemented route groups:

- cycles and lifecycle transitions;
- models, model versions and plan versions;
- sparse grid query/write-batch/spread/paste-preview;
- scenarios;
- assumptions and values;
- baselines, revisions, transfers and budget control;
- workforce, CapEx, revenue drivers, working capital and FX;
- actual refresh run status and variance query;
- calculation/recalculation and commentary.

## Development seed

The disposable/development seed provides, per tenant:

- one FY2027 monthly annual-budget cycle;
- one active integrated model and published model version;
- one published base plan version and shared Base scenario;
- Revenue input of 150,000 EGP for 2027-01;
- a published 150,000 EGP baseline and Warn control bucket;
- aggregate workforce planning of 12 FTE at 18,000 EGP average cost;
- one 45,000 EGP prioritized CapEx item;
- one 100 × 1,500 EGP revenue driver, working-capital assumptions and USD/EGP planning FX.

## Permission boundary

The administrator seed role receives the canonical Module 28 permissions. Other roles do not receive planning access by default. The frontend must treat permission results as visibility guidance only; the API remains authoritative and must handle 401/403/lifecycle errors without technical details.

## Not yet safe to build against as final product scope

Submission slices, department approval, dimension-member administration, source mappings, formula authoring, spreading/allocation, integrated statement projections, cross-module source adapters, Excel interop, and full accuracy/roll-up reporting remain backend gaps. Do not create UI claims for those capabilities until the backend matrix is updated with PASS evidence.
