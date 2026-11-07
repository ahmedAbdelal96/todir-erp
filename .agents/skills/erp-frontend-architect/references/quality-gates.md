# TODIR ERP Quality Gates & Verification Ladder

In TODIR ERP, "It compiles" or "unit tests pass" is only the beginning. The Product Owner requires zero defect rounds. A delivery can only be reported as complete after passing through this exact verification ladder.

---

## 1. The 7-Step Verification Ladder

Every frontend mission must execute and report these exact 7 commands:

| Step | Gate | Exact Command | Success Criteria |
|:---:|:---|:---|:---|
| **1** | **Typecheck** | `pnpm run typecheck` | 0 errors across root and `frontend/`. |
| **2** | **Architecture Boundaries** | `pnpm run architecture:frontend` | Layer boundaries, public barrels, and module fixture rules PASS. |
| **3** | **Localization Parity** | `pnpm run localization:verify` | 100% AR/EN key parity; zero hardcoded UI copy detected by AST. |
| **4** | **API Drift** | `pnpm run api:drift` | 64 API paths validated with zero drift against backend OpenAPI spec. |
| **5** | **Frontend Slice Tests** | `pnpm run test:frontend` | All slice tests, formatters, and domain invariant assertions PASS. |
| **6** | **Production Build** | `pnpm --dir frontend build` | Vite production bundle compiles cleanly with no syntax errors. |
| **7** | **Real-Stack E2E (Playwright)** | `pnpm --dir frontend test:e2e tests/e2e/<flow>.spec.ts` | All browser journeys execute and pass against real API & PostgreSQL. |

---

## 2. Visual & Layout Inspection Checklist

Open and inspect every screen in the browser or via Playwright screenshots:

- [ ] **Design System Contrast**:
  - Topbar header is authentic Dark Oxford Navy (`#102a43`).
  - Page canvas is cool neutral slate (`#f0f4f8`).
  - All cards, tables, and dialogs are pure white (`#ffffff`) with solid perimeter borders (`#cbd5e1`).
  - Zero low-contrast "ghost" screens.
- [ ] **Zero Mixed View/Form Collision**:
  - List pages contain ONLY tables, filters, search, and KPI summaries.
  - Creation/editing happens exclusively in centered Modal Dialogs or dedicated Creation Routes.
  - Clicking a table row opens the Detail Drawer without navigating away.
- [ ] **Arabic RTL & Bidi Text**:
  - Document direction is `dir="rtl"`.
  - All reference codes (`INV-2026-001`, `PO-099`), phone numbers, and dates are isolated inside `<BdiText>` with clean punctuation.
  - Directional icons (arrows, chevrons) are flipped; non-directional icons (checks, search) are not flipped.
- [ ] **Responsive & Mobile Viewport (390px)**:
  - Test at 390px (iPhone viewport): zero horizontal page scrollbar; cards and metric ribbons stack gracefully.
- [ ] **The 6 Mandatory UI States**:
  - Verified loading skeletons on fetch.
  - Verified guided empty state when table has 0 rows.
  - Verified actionable error state with retry button on API failure.
  - Verified calm no-permission message when RBAC denies access.
- [ ] **English LTR Mode**:
  - Click language switcher: layout flips cleanly to `dir="ltr"`, 100% translated English copy, zero stray Arabic strings.

---

## 3. Honest Reporting Protocol

- **Never claim a check passed that you did not actually execute.**
- In the final report, list each gate as **PASSED**, **FAILED**, or **NOT RUN**.
- If a gate fails, fix the root cause locally and re-run the verification chain.
- Product Owner review is the final approval of verified work, not a defect-finding step.
