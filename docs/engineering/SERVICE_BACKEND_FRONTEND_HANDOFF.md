# Module 24 Backend → Frontend Handoff

Backend handoff is ready after the final gate on `dev`.

## Primary API roots

`/api/v1/service` exposes requests, work orders, visits, assignments, installed base, templates, checklists, diagnosis, maintenance plans/due, meter readings, warranties, contracts/entitlement, SLA foundation, parts, labor, downtime, reports, charges/invoice seams, dispatch, mobile day, and offline commands.

## Product-facing guarantees

- Use `rowVersion` from requests, work orders, visits, and installed-base records for optimistic mutation.
- Treat `409` problem responses with the returned `code` as a conflict requiring refresh or human resolution.
- Offline commands are replay-safe by `clientCommandId`; `Conflict` is a visible resolution state, never a silent overwrite.
- A work order cannot reach technical completion while required checklist answers or unresolved part quantities remain.
- Billing owner `Projects` requires a project reference; Service does not duplicate Project or Invoice truth.
- Customer field service requires a customer and an installed-base subject.
- Use the Arabic ERP glossary and RTL rules from the frontend manual; backend status values are API language, not user copy.

## Suggested first frontend journeys

1. Dispatcher: ready work orders → visit → assignment → mobile day.
2. Technician: visit lifecycle → checklist/diagnosis/parts/labor → report → customer acceptance.
3. Maintenance manager: plan → due → convert → work order → meter/downtime history.
4. Service manager: contract/warranty → entitlement → charges → invoice request/allocation.

Frontend owns layout, Arabic copy, responsive behavior, and product acceptance. Backend owns tenant isolation, lifecycle truth, persistence, audit, idempotency, and cross-module ownership boundaries.
