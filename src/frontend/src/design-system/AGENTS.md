# Design System Governance (`src/frontend/src/design-system/`)

## Identity and responsibility

The `design-system/` directory owns the reusable UI presentation foundation, design tokens, OKLCH theme compiler, density engine, accessible UI primitives, domain-specific formatting engines, form layout system, DataGrid wrapper, and standard page archetypes.

Core principle:
> **`design-system/` is strictly domain-neutral, brand-agnostic, and 100% Free Open Source. It contains zero business logic, zero domain-specific terminology, and never imports business modules.**

---

## 1. Absolute filename prohibition

Never create or introduce a new file or directory whose basename contains `todir` in any casing. Use descriptive kebab-case names (e.g. `money-field.tsx`, `data-grid.tsx`, `document-page.tsx`).

---

## 2. Architectural rules for `design-system/`

1. **Zero Business Module Dependencies:** `design-system/` MUST NOT import anything from `src/frontend/src/modules/` (enforced by AST boundary check).
2. **Domain-Neutral Component Naming:** Component names and props must describe interaction/visual semantics, never business domain entities.
   - Allowed: `MoneyField`, `QuantityField`, `StatusBadge`, `DocumentPage`, `DataGrid`.
   - Forbidden: `InvoiceField`, `JournalGrid`, `PatientCard`, `SalesTable`.
3. **100% Free-Only Core Rule:**
   - Primitives encapsulate unstyled `react-aria-components` (Apache-2.0).
   - DataGrid encapsulates `ag-grid-community` (MIT). Commercial engines (`ag-grid-enterprise`, MUI Pro/Premium) are strictly prohibited.
4. **Token & OKLCH Theme Architecture:**
   - Styling is driven by design tokens (`tokens/`) compiled to CSS custom properties.
   - Theme customization uses `ThemeDefinition` validated via Zod and compiled through perceptual OKLCH color math (`color-derivation.ts`). Arbitrary tenant CSS injection is strictly rejected.
   - Density engine (`comfortable` vs `compact`) is decoupled from color themes.
5. **Exact Financial Value Contracts:**
   - All financial/domain formatting (`formatting/`) operates on exact string decimals (`DecimalString`). Zero JavaScript binary floating-point (`number`) arithmetic for financial truth.
6. **Page Archetypes:**
   - Standardizes `ListPage`, `RecordPage`, `DocumentPage`, `SettingsPage`, and `WizardPage`.
   - `DocumentPage` must support independent multi-axis status badges (`lifecycle`, `approval`, `compliance`, `accounting`, `settlement`, `isLocked`).
7. **Promotion Criteria to Design System:**
   - Before moving any component into `design-system/`, it must be proven that: (a) semantics are domain-neutral, (b) behavior and accessibility are broadly reusable, (c) API is free of module terminology, and (d) an interactive Storybook story (`*.stories.tsx`) and unit test are included.
8. **Testing & Storybook:** Every component in the design system must have Storybook coverage and pass automated Axe-core accessibility checks.
