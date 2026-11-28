# TODIR ERP — Module 28: Budgeting, Financial Planning & Analysis (FP&A)
## Frontend Implementation & Product/UX Acceptance Matrix

- **Module ID**: `planning` (Module 28)
- **Canonical Specification**: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- **Backend Handoff Baseline**: `docs/engineering/BUDGETING_BACKEND_FRONTEND_HANDOFF.md` (`c02f12e5c33f5f0ebd5e3866d447b79eb3b124d6`)
- **OpenAPI Contract**: `docs/api/planning-openapi.json` (91 validated paths under `/api/v1/planning`)
- **Delivery Status**: `FRONTEND COMPLETE — PRODUCT / UX ACCEPTANCE COMPLETE — READY FOR PRODUCT OWNER SIGN-OFF`

---

## 1. Canonical Navigation & Permission Alignment (`finance` Category)

All 9 Planning workspaces are registered in `frontend/app/router/destination-registry.ts` (`PLANNING_DESTINATIONS`), `frontend/app/router/navigation-registry.ts`, `frontend/app/router/app-router.tsx`, and `frontend/modules/planning/routes.tsx`, strictly gated against `backend/Modules/Planning/module.manifest.json` permissions and `ModuleLifecycle` ReadOnly/NotEntitled rules.

| Destination ID | Route | Arabic Label | English Label | Primary Permissions |
|---|---|---|---|---|
| `planning-overview` | `/planning` | النظرة المالية الشاملة | Financial Overview | `planning.versions.read`, `planning.budget.read`, `planning.variance.read`, etc. |
| `planning-budgets` | `/planning/budgets` | دورة حياة الموازنة | Budget Lifecycle | `planning.cycles.read`, `planning.versions.read` |
| `planning-grid` | `/planning/grid` | طاولة التخطيط المالي | Planning Workspace Grid | `planning.versions.read`, `planning.budget.read` |
| `planning-models` | `/planning/models` | النماذج التشغيلية والمحركات | Planning Models & Drivers | `planning.models.read`, `planning.workforce.read`, `planning.capex.read` |
| `planning-forecasts` | `/planning/forecasts` | التوقعات والسيناريوهات | Forecasts & Scenarios | `planning.versions.read`, `planning.variance.read` |
| `planning-analysis` | `/planning/analysis` | التقارير وتحليل الانحرافات | Statements & Variance Analysis | `planning.variance.read`, `planning.versions.read` |
| `planning-budget-control` | `/planning/budget-control` | الرقابة على الموازنة والالتزامات | Budget Control & Overrides | `planning.budget_control.read` |
| `planning-submissions` | `/planning/submissions` | موازنات الأقسام والاعتمادات | Department Submissions & Targets | `planning.versions.read`, `planning.budget.read` |
| `planning-config` | `/planning/config` | إعدادات النماذج والمقاييس | Planning Configuration | `planning.models.manage`, `planning.cycles.manage` |

---

## 2. Capability & UX Acceptance Coverage

| Capability Area | Frontend Implementation | Automated / Real-Stack Verification | Status |
|---|---|---|---|
| **CFO Financial Overview & KPIs** | `planning-overview-page.tsx` | `UX-T01` (Real-stack Release API + `todir_planning_acceptance`) | PASS |
| **SME Simple Annual Budget Flow** | `simple-annual-budget-modal.tsx` | `UX-T02` (Modal dialog, `MoneyField`, `LocalDateField`) | PASS |
| **Budget Lifecycle, Revisions & Transfers** | `planning-budgets-page.tsx` | `UX-T03` (Cycles, versions, original baseline lock, copy draft) | PASS |
| **Spreadsheet Grid, Explicit Zero & Excel Interop** | `planning-grid-page.tsx` | `UX-T04` (Atomic batch save, spread, recalculate, XLSX export/template) | PASS |
| **Operational Models & Drivers (OPEX, Rev, WF, CapEx, WC/FX, Assumptions)** | `planning-models-page.tsx` | `UX-T05` (Seeded 12 FTE Workforce, 45k CapEx, 2 OPEX, Revenue, WC, USD/EGP 52) | PASS |
| **Rolling Forecasts & Scenarios** | `planning-forecasts-page.tsx` | `UX-T06` (Scenario comparison, rolling forecast generation, accuracy) | PASS |
| **Variance Analysis & Projected Statements** | `planning-analysis-page.tsx` | `UX-T07` (Management projection disclaimer, P&L / BS / CF projections) | PASS |
| **Budget Control, Availability & Overrides** | `planning-budget-control-page.tsx` | `UX-T08` (Warn policy, 150k EGP bucket, availability check, reconcile) | PASS |
| **Cross-Module Budget Control UX (Purchasing & Expenses)** | `purchasing-error-map.ts`, `expenses-error-map.ts` | Mapped `planning.budget_approval_required`, `planning.budget_exceeded`, `planning.budget_bucket_not_found` | PASS |
| **Department Submissions, Targets & Proposals** | `planning-submissions-page.tsx` | `UX-T09` (Submission units, lock/unlock, top-down targets, proposals) | PASS |
| **Configuration (Dimensions, Account Mappings, Formulas)** | `planning-config-page.tsx` | `UX-T10` (Dimensions, GL/Metric mappings, Formula catalog & validation) | PASS |
| **Arabic RTL / English LTR & Responsive Viewports** | `ar.ts` / `en.ts` (1:1 parity, zero machine translations) | `UX-T11` + `localization:verify` + `planning-slice.test.ts` | PASS |
| **Permission & Lifecycle ReadOnly Protection** | `use-planning-access.ts`, `routes.tsx` | `UX-T12` + `permissions-manifest-audit.test.ts` | PASS |

---

## 3. Verification Commands Executed (After Final Edit)

- `pnpm --dir frontend run typecheck` — PASS (0 errors)
- `pnpm run architecture:frontend` (module boundaries, 10 fixtures, `localization:verify`) — PASS
- `pnpm run api:drift` — PASS
- `pnpm run test:frontend` — PASS (300 / 300 tests, including `planning-slice.test.ts` & `permissions-manifest-audit.test.ts`)
- `pnpm --dir frontend run build` — PASS (Vite production bundle)
- `pnpm --dir frontend exec playwright test --config=playwright.planning.config.ts` — PASS (11 / 11 real-stack E2E tests against Release `Api.dll` + disposable `todir_planning_acceptance` PostgreSQL DB, zero route mocks)
