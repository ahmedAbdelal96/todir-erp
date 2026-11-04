# Module 18 Payroll Frontend Acceptance

Canonical pack: `docs/modules/18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This document records frontend-owned acceptance evidence for the supported Payroll journeys. It does not replace the backend financial acceptance state owned by the Payroll backend execution document.

## Delivered frontend scope

- Real legal-entity context is required; the Payroll page no longer invents a fallback entity identifier.
- Payroll API reads surface authoritative server errors. Components, salary structures, and assignments no longer fall back to fabricated rows.
- Inputs and Loans tabs call their read contracts with server-side search/status parameters. The backend read contracts are now available; the frontend still preserves the explicit localized contract-unavailable state for genuine API errors.
- Arabic/English labels are parity-checked, including setup tables and payslip breakdown labels.
- Employee display formatting never turns an opaque UUID into a user-facing employee label.
- Runtime context has an optional legal-entity accessor for screens rendered outside the shell provider; a missing context produces an actionable empty state.

## Runtime acceptance evidence

Executed against the local Vite frontend, local API, PostgreSQL-backed development data, and the seeded authenticated session:

| Journey | Evidence |
| --- | --- |
| Payroll overview and workspace | Playwright test 1 passed; authenticated page rendered with the selected legal entity. |
| Groups, periods, setup dialogs | Playwright tests 2–3 passed; creation dialogs opened with localized date/pay-day labels. |
| Setup sub-tabs | Playwright test 4 passed against the prior backend baseline; live API smoke after contract delivery returned 200 for components, salary structures, assignments, inputs, and employee receivables with persisted identities. A fresh frontend Playwright rerun is the remaining frontend-owner verification. |
| Statutory settings | Playwright test 5 passed. |
| Runs and workspace | Playwright tests 6 and 14 passed. |
| Inputs/import and loans | Playwright tests 7–8 passed; import preview and creation dialogs work. Read history contracts are now delivered and return persisted rows; frontend-owner rerun remains pending. |
| Payslips/reporting/opening balances | Playwright tests 9–11 passed; real API data, pagination, GL status, export scope, and opening-balance dialog were exercised. |
| Responsive and localization | Playwright tests 12–13 passed for 390px mobile and English LTR. |
| Persisted lifecycle, accounting and isolation | Playwright test 15 passed: run lifecycle, persisted reconciliation/journal state, CSV export, unauthenticated 401, missing-run 404, and foreign-entity 403 were asserted. |

## Verification commands

- `pnpm run typecheck` — PASS.
- `pnpm --dir frontend build` — PASS (Vite production build; chunk-size warning only).
- `pnpm run architecture:frontend` — PASS.
- `pnpm run test:frontend` — PASS: 182 tests, 182 passed, 0 failed.
- `pnpm run api:drift` — PASS: 64 API paths validated.
- `SEED_DEFAULT_PASSWORD=... pnpm --dir frontend exec playwright test tests/e2e/payroll-flow.spec.ts --workers=1` — PASS: 15 passed, 0 failed.

## Backend contract handoff

The Payroll backend now exposes paginated, searchable, status-filtered GET contracts for components, salary structures, employee assignments, payroll inputs, and employee receivables. The live API proof returned HTTP 200 for all five routes against the PostgreSQL-backed development seed; assignments, inputs, and receivables included HR-resolved employee names/numbers, and inputs included component code/name. Backend implementation and financial acceptance remain owned by the backend mission. A fresh frontend Playwright rerun is required before frontend acceptance is upgraded from the previously recorded baseline.

