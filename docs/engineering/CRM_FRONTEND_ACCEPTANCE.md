# Module 19 CRM (Customer Relationships) Frontend Acceptance

Canonical pack: `docs/modules/19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This document records frontend-owned acceptance evidence for the supported CRM seller, manager, and administrative journeys. It does not replace the backend execution document (`docs/engineering/CRM_BACKEND_EXECUTION_STATE.md`).

## Delivered Frontend Scope

- **High-Contrast Design System Compliance (Zero Tailwind Utility Classes)**: Completely eliminated uncompiled Tailwind utility classes (`grid`, `bg-white`, `p-4`, `rounded-lg`, etc.). Fully codified with authentic Dark Oxford Navy header anchor (`#102a43`), cool neutral slate background canvas (`#f0f4f8`), and crisp pure white surface cards (`#ffffff`) with solid perimeter borders (`#cbd5e1`) and dual-layer elevation shadows using ToDir design tokens and reusable primitives (`Surface`, `CrmKpiCard`, `Table`, `Button`, `Dialog`, `TextField`, `Select`, `Checkbox`). Low-contrast ghost screens and unstyled HTML elements are strictly eliminated.
- **Strict Separation of Concerns (Zero Mixed View/Form Collision)**: Data browsing screens (tables, Kanban boards, 360-degree profiles) are cleanly separated from data entry and modification. All mutations are triggered via explicit Primary Action buttons opening dedicated Centered Modal Dialogs (`Dialog` primitive) with background overlay shielding.
- **Full Arabic RTL & English LTR Parity**: 100% key parity between `frontend/modules/crm/i18n/ar.ts` and `en.ts` with natural business Arabic terminology, authentic LTR direction toggling, and zero raw developer jargon or exposed technical GUIDs.
- **My Sales Today (Seller Home)**: Real-time prioritized worklist featuring due/overdue follow-ups, new uncontacted leads, stale deals (>14 days without activity), authentic KPI cards with distinctive semantic color tones, and immediate inline action triggers.
- **Lead Inbox & Lifecycle**: Dedicated lead management with status sub-navigation pills (All, New, Working, Qualified, Disqualified, Converted), server-side search, scope toggles (My Leads vs. All Leads), Qualify modal documenting BANT requirements, Disqualify modal with mandatory reasons, Convert Lead modal creating commercial parties and linked pipeline opportunities (with authentic user-entered deal amounts, zero fabricated defaults), Duplicate Merge modal, and CSV import modal.
- **Opportunity Pipeline (Kanban & List)**: Interactive multi-stage Kanban board with stage progress bars, deal health badges (`Healthy`, `AtRisk`, `Stale`), probability-weighted stage totals, and instant view-mode toggle to structured data table.
- **Opportunity 360 Workspace**: Comprehensive record inspector supporting Buying Committee role matrix (Champion, Decision Maker, Economic Buyer, Influencer, Gatekeeper, Technical Evaluator), Commercial Interests tracking with product catalog linking, and Quotation Handoff transition into Module 17 Sales.
- **Activities & Follow-ups**: Centralized activity management for Calls, Meetings, Follow-ups, and Tasks with database-aligned state filtering (`Scheduled`, `Completed`), overdue warnings, and rapid completion dialog documenting outcomes and scheduling subsequent next actions.
- **Sales Management & Forecasting**: Revenue projections across Pipeline, Best Case, and Commit categories, separate Opportunity Win Rate (`reportsSummary.opportunityWinRate`) and Lead Conversion Rate (`reportsSummary.leadConversionRate`) KPI cards with locale formatting, pipeline inspection table, sales team hierarchy management, pipeline/stage designer, and custom field configuration with backend TitleCase type enforcement.

## Resolved Root Causes & Data Alignment

1. **Design System Migration**: Replaced all Tailwind utility class layouts across all CRM tabs and modals with ToDir's codified design system components (`Surface`, `CrmKpiCard`, `Table`, `Button`, `TextField`, `Select`, `Checkbox`, `Dialog`), CSS variables (`var(--color-surface-card)`, `var(--color-border-default)`, `var(--shadow-sm)`), and flex/grid style helpers.
2. **PostgreSQL Activity State Parity**: Normalized activity states between frontend (`Pending`/`Scheduled`) and the backend `business_crm.crm_activity` schema (`state = 'Scheduled'`), ensuring open activities render and complete accurately.
3. **Custom Field TitleCase Validation**: Updated `new-custom-field-modal.tsx` to emit TitleCase data types (`Text`, `Integer`, `Decimal`, `Boolean`, `Date`) required by backend validation (`CrmRepository.cs`), resolving 400 Bad Request rejections.
4. **Authoritative Deal Health Status & Badges**: Aligned DealHealthBadge, contract types, and translation keys with the authoritative backend contract (`Healthy`, `Good`, `Fair`, `AtRisk`, `Stale`), eliminating missing label fallbacks.
5. **Separation of Opportunity Win Rate & Lead Conversion Rate**: Fixed the management dashboard to bind the Win Rate KPI strictly to `reportsSummary.opportunityWinRate` (deals won / total closed deals) while displaying Lead Conversion Rate strictly from `reportsSummary.leadConversionRate` (leads converted / total leads) with distinct Arabic and English labels. Added automated assertions ensuring zero-denominator division produces `0.00%` rather than NaN or 100%.
6. **KPI Counting & Missing Financial Data Invariants**: Seller home counts now query full server-side counts (`totalCount`) rather than inferring from paginated client arrays. Formatted missing or null financial values safely as em-dash (`"—"`) instead of silently fabricating zero.
7. **Elimination of Fabricated Financial Defaults**: Removed hardcoded prefilled amounts in lead conversion and opportunity modals; users specify authentic financial values or leave them unassigned.

## Runtime Acceptance Evidence

Executed against the local Vite frontend, local .NET API daemon, PostgreSQL database in Docker (`todir-postgres-1`), and authenticated session:

| Test # | Journey | Persisted Workflow & Verification |
| --- | --- | --- |
| 01 | Visual & Layout Architecture Verification | Oxford Navy header `#102a43`, Slate background `#f0f4f8`, Surface cards `#ffffff`, borders `#cbd5e1`, semantic KPI cards verified via computed CSS. |
| 02 | Lead Management (Capture & Persistence) | New Lead modal opened, form fields entered, persisted to backend database, verified in lead table. |
| 03 | Lead Qualification (BANT Criteria) | Qualify modal captures budget, authority, need, and timeline; transitions lead state to Qualified in backend. |
| 04 | Lead Conversion | Convert modal links or creates commercial party and pipeline opportunity without fabricated defaults. |
| 05 | Opportunities Pipeline (Kanban & List Toggle) | Kanban board rendered with stage columns, probability, deal cards, and view-mode toggle to data table. |
| 06 | Opportunities Creation | Dedicated modal dialog captures deal title, authentic numeric value, pipeline, and initial stage. |
| 07 | Opportunity Workspace (360 View & Committee) | Full 360 inspector, Buying Committee role matrix addition, commercial interests, and Quotation handoff. |
| 08 | Activities Management (Schedule & Complete) | Schedule activity, persist as Scheduled, complete with outcome note and next follow-up action. |
| 09 | Sales Management (Forecasts, Teams, Custom Fields, Metrics) | Revenue forecasts across categories, team creation, custom field definition, and distinct Opportunity Win Rate vs Lead Conversion Rate KPIs with separate Arabic/English business labels verified. |
| 10 | Lead Inbox (Search, Filtering & Pagination) | Server-side search, status pill filtering, scope toggling, and data navigation verified. |
| 11 | Mobile Viewport (390px) | Responsive layout on iPhone viewport (390x844), verified zero horizontal overflow or clipping. |
| 12 | English LTR Parity | Toggle via `button.topbar-lang-btn`, verify `dir="ltr"`, complete English typography, and zero missing copy. |

## Verification Suite Results

- `pnpm --dir frontend typecheck` — PASS (0 errors).
- `pnpm run localization:verify` — PASS: resource parity and hardcoded-copy guard clean.
- `pnpm run architecture:frontend` — PASS: module boundaries, alias rules, and all 9 architectural fixtures clean.
- `pnpm run test:frontend` — PASS: 192 tests, 192 passed, 0 failed (includes metric separation, zero-denominator, and deal health slice tests).
- `pnpm run api:drift` — PASS: 64 API paths validated with zero drift.
- `pnpm --dir frontend build` — PASS: Vite production build clean in 1.35s.
- `pnpm --dir frontend test:e2e tests/e2e/crm-flow.spec.ts` — PASS: 12 tests passed, 0 failed (53.9s against real backend & PostgreSQL).

## Frontend Delivery Status

`MODULE 19 CRM FRONTEND COMPLETE — METRICS CORRECT — FINAL UX ACCEPTANCE PASSED`
