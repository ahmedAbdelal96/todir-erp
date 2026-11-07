# Frontend Project Profile: TODIR ERP SaaS
<!-- Location in repo: .erp-frontend/profile.md. Keep under ~150 lines. Facts only.
     Updated by the agent at the end of any task that changes a convention, shared component, permission code or glossary term. -->

last_verified: <YYYY-MM-DD>   verified_by: <agent/owner>

## 1. Product
- name: TODIR ERP SaaS            client/industry: Production-grade multi-tenant enterprise ERP SaaS
- domain pack: docs/modules/*.md
- markets: MENA / Arab world (Egypt, Saudi Arabia, UAE, etc.)      languages: ar, en      primary: ar (RTL-native)
- arabic_register: Modern Standard Arabic (Business Market Arabic), natural, professional, zero literal translations
- multi-tenant: yes (strict PostgreSQL RLS)         multi-currency: yes      multi-company / multi-entity: yes (Legal Entities)

## 2. Stack
- framework: Vite + React Router (SPA)              language: TypeScript (strict: yes)
- styling: Codified High-Contrast Corporate Design System (CSS Variables + Design Tokens). **ZERO TAILWIND UTILITY CLASSES.**
  - Header anchor: Dark Oxford Navy `#102a43`
  - Page canvas: Cool neutral slate `#f0f4f8`
  - Surface cards: Pure crisp white `#ffffff` with solid perimeter borders `#cbd5e1` and dual-layer elevation shadows
- server state: TanStack Query                     forms: react-hook-form + zod
- tables: Shared Table / DataGrid (`frontend/components/primitives/table.tsx`, `frontend/components/grid/`)
- backend: .NET 10 (Modular Monolith)              api style: REST / OpenAPI (httpClient in `frontend/core/api/`)
- package manager: pnpm                            node: v24.15.0

## 3. Commands (exact)
- dev: `pnpm --dir frontend dev`
- typecheck: `pnpm run typecheck`
- test: `pnpm run test:frontend`
- architecture: `pnpm run architecture:frontend`
- localization: `pnpm run localization:verify`
- api drift: `pnpm run api:drift`
- build: `pnpm --dir frontend build`
- e2e: `pnpm --dir frontend test:e2e tests/e2e/<flow>.spec.ts`

## 4. Structure
```text
frontend/
├── app/          # App bootstrap, router, providers, AppShell, Oxford Navy Topbar
├── core/         # Auth, session, tenant context, HTTP client, queryClient, i18n
├── components/   # Single source of truth for all UI components & design system
│   ├── fields/       # Domain fields (MoneyDisplay, StatusBadge, etc.)
│   ├── forms/        # Form layouts, FormField, FormSection, ValidationSummary
│   ├── formatting/   # quantity-formatter.ts, money formatting, bidi-text.tsx
│   ├── grid/         # DataGrid, table columns, cell renderers
│   ├── pages/        # Reusable page archetypes (ListPage, RecordPage, Surface)
│   └── primitives/   # Button, Dialog, Select, ComboBox, Table, Pagination, Icons
├── modules/      # Canonical business slices (crm, sales, purchasing, accounting, inventory, etc.)
└── tests/        # E2E (Playwright), localization checks, architecture fixtures
```

## 5. Shared components (use these; never re-implement)
| Need | Import path | Notes |
|---|---|---|
| Card / Container | `frontend/components/pages/surface.tsx` | Pure white `#ffffff`, solid border `#cbd5e1`, shadow |
| Data table | `frontend/components/primitives/table.tsx` or `grid/data-grid.tsx` | Server pagination, sticky header, RTL |
| Pagination | `frontend/components/primitives/pagination.tsx` | Numbered pager with ellipsis |
| Metric ribbon / KPI | `frontend/components/primitives/crm-kpi-card.tsx` | High-contrast semantic KPI cards |
| Inputs / Textfield | `frontend/components/primitives/text-field.tsx` | Accessible with label and error binding |
| Select / Dropdown | `frontend/components/primitives/select.tsx` | Accessible dropdown |
| Modal dialog | `frontend/components/primitives/dialog.tsx` | Centered dialog with backdrop overlay |
| Button | `frontend/components/primitives/button.tsx` | Primary, secondary, outline, destructive |
| Badge / Status | `frontend/components/fields/status-badge.tsx` | Semantic status badges (Healthy, Stale, etc.) |
| Formatting | `frontend/components/formatting/quantity-formatter.ts` | Currency, numbers, percentages, dates |
| Bidi text isolation | `frontend/components/formatting/bidi-text.tsx` | Isolates Latin codes and numbers in Arabic sentences |

## 6. Conventions
- permissions: Central RBAC evaluated via server session claims and permission authorizer. UI buttons check permissions gracefully.
- currency: Authoritative from backend DTO (`currencyCode`) or legal entity base currency. Format via `formatCurrency` in `quantity-formatter.ts`. Never hardcode `SAR` or `EGP`. Missing values show `"—"`.
- digits: Latin digits (`0-9`) in financial/accounting tables with Western thousand separators; text in natural Arabic.
- i18n: `i18next` with complete parity between `modules/<module>/i18n/ar.ts` and `en.ts`. No hardcoded strings.
- states: Every view must handle all 6 states: loading skeleton, guided empty state, actionable error, no permission, partial data, stale data.
- mutations: Disable submit on click, send idempotency key where supported, close modal/drawer on success, refetch queries.
- immutability: Draft records can be deleted; approved/posted records are immutable (require adjustment, credit note, or reversal with reason).

## 7. Mandatory Rules
1. Zero Tailwind utility classes. Strict adherence to TODIR Design System tokens.
2. Strict Separation of Concerns: Lists and tables are strictly for browsing; mutations happen via dedicated Modal Dialogs (`Dialog`) or dedicated Creation Pages.
3. Every final delivery must pass the 7 verification gates (`typecheck`, `test:frontend`, `architecture:frontend`, `localization:verify`, `api:drift`, `build`, and real Playwright E2E).
