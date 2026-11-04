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
| Setup sub-tabs | Playwright test 4 passed with positive assertions: components (`BASE`), salary structures (`STANDARD-EG`), and assignments (resolved employee names) render live rows. Modals open cleanly and close without errors. |
| Statutory settings | Playwright test 5 passed. |
| Runs and workspace | Playwright tests 6 and 14 passed. |
| Inputs/import and loans | Playwright tests 7–8 passed; import preview and creation dialogs work. Real inputs and employee receivables render with resolved names and component labels; action approvals verified. |
| Payslips/reporting/opening balances | Playwright tests 9–11 passed; real API data, pagination, GL status, export scope, and opening-balance dialog were exercised. |
| Responsive and localization | Playwright tests 12–13 passed for 390px mobile and English LTR. |
| Persisted lifecycle, accounting and isolation | Playwright test 15 passed: run lifecycle, persisted reconciliation/journal state, CSV export, unauthenticated 401, missing-run 404, and foreign-entity 403 were asserted. |

## Verification commands

- `pnpm --dir frontend typecheck` — PASS.
- `pnpm --dir frontend build` — PASS (Vite production build; clean).
- `pnpm run localization:verify` — PASS: resource parity and hardcoded-copy guard clean.
- `pnpm run architecture:frontend` — PASS: boundary rules, 9 fixtures, and localization pass.
- `pnpm run test:frontend` — PASS: 182 tests, 182 passed, 0 failed.
- `pnpm run api:drift` — PASS: 64 API paths validated.
- `SEED_DEFAULT_PASSWORD=... pnpm --dir frontend test:e2e tests/e2e/payroll-flow.spec.ts` — PASS: 15 passed, 0 failed.

## Frontend Integration Complete

All 5 backend read contracts (components, salary structures, employee assignments, payroll inputs, and employee loans/receivables) are fully integrated with live paginated lists, status filters, search, and dedicated creation dialogs. All contract-unavailable alerts are removed. All employee identities resolve to real HR names and employment numbers without raw GUIDs. Universal modal centering (`display: flex; align-items: center; justify-content: center; margin: auto`) guarantees perfect centering across all viewports and RTL/LTR modes with popover z-index `2500` rendering above modal overlays.

