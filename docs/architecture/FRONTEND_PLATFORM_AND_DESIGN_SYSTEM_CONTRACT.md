# Frontend Platform & Design System Contract

**Status:** IMPLEMENTATION CONTRACT — APPROVAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-08  
**Scope:** Frontend platform, design system, theme engine, runtime context, API boundary, module UI architecture, accessibility, Arabic/RTL, testing, implementation sequence  
**Primary application:** Authenticated ERP web application  
**Architecture:** React + TypeScript + Vite SPA inside the existing monorepo  
**Audience:** Product Owner, Senior Product Architect, Senior UX/Product Designer, Senior Frontend Architect, Senior React/TypeScript Engineer, Accessibility Engineer, Frontend Performance Engineer, QA Engineer, Implementation Agent  
**Risk Class:** CRITICAL FOUNDATION — mistakes here multiply across every future module.

---

# 0. Mandatory Agent Role

The implementation agent MUST act as a senior cross-functional team:

- Senior Product Architect.
- Senior Software/Systems Architect.
- Senior Product/UX Designer.
- Senior React/TypeScript Engineer.
- Senior Design-System Engineer.
- Senior Accessibility Engineer.
- Senior Frontend Performance Engineer.
- Senior Security Engineer.
- Senior QA/Test Engineer.
- Senior Production Engineer.

The agent MUST reason business-first and protect approved architecture, domain ownership, permissions, tenant isolation, Arabic/RTL, performance, accessibility and maintainability.

Core principles:

> **Simple Outside. Powerful Inside.**

> **Build one coherent product language and runtime once, then compose business modules from it.**

> **Future-proof contracts; do not future-build product.**

---

# 1. Governing Documents and Precedence

Before changing frontend code, read:

1. root `AGENTS.md`;
2. `src/frontend/AGENTS.md`;
3. any nearer scoped `AGENTS.md`;
4. `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`;
5. `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`;
6. `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`;
7. this contract;
8. current ADRs;
9. the relevant business-module pack before implementing that module UI.

Repository files are authoritative execution context.

If governing documents conflict, DO NOT silently choose. Execute the governance migration in §7 first.

---

# 2. Why This Contract Exists

The ERP is not a collection of CRUD screens. It is a multi-tenant, multi-entity, modular platform with:

- independently activatable modules;
- multiple legal entities/branches;
- strict permissions and field security;
- role-specific experiences;
- immutable posted/finalized states;
- tenant theming;
- Arabic and English;
- accountant-dense workflows;
- mobile operational workflows;
- heavy grids;
- audit/history;
- future Studio/custom fields;
- generated API contracts.

Without a platform contract, modules would invent separate money inputs, tables, statuses, loading states, themes, permissions UX and RTL fixes. That is forbidden.

---

# 3. Final Product Goal

Users must feel Directory, Catalog, Sales, Finance, Payroll, Inventory, Shipping and future modules are parts of one product.

```text
Common Frontend Platform
        │
        ├── Runtime Context
        ├── API/Data Platform
        ├── Theme Engine
        ├── Design Tokens
        ├── UI Primitives
        ├── Domain Fields
        ├── Forms
        ├── Data Grid
        ├── Page Archetypes
        ├── Entity Navigation
        ├── Permission/Capability Patterns
        └── Testing Rules
                │
                ▼
        Business Module UIs
```

---

# 4. Brand-Agnostic Architecture — LOCKED

Reusable frontend foundation MUST NOT be named after the commercial brand.

Forbidden:

```text
todir-ui
todir-tokens
todir-icons
TodirButton
TodirDataGrid
TodirMoneyField
```

The commercial brand is configuration/data, not architecture.

Allowed concepts:

```text
BrandProfile
ProductIdentity
ThemeDefinition
TenantAppearance
LegalEntityDocumentBranding
```

Changing product name MUST NOT require renaming foundational packages/components.

---

# 5. Technical Naming Rule

- Existing repository/application identifiers may remain `todir-web` while brand is TODIR.
- Reusable frontend foundation package names MUST be brand-neutral.
- Business module IDs remain canonical domain IDs (`sales`, `inventory`, `directory`, etc.).
- Visible product/module labels come from localization/brand configuration.
- Do not perform repository-wide renaming merely to remove TODIR from infrastructure identifiers unless separately approved.

---

# 6. Free-Only Core Dependency Rule — LOCKED

Core MUST NOT require any paid runtime library, paid UI license or paid SaaS to:

- build;
- run locally;
- run CI;
- deploy;
- operate standard ERP functionality;
- add standard modules;
- use the standard grid/forms/theme/testing stack.

> **No paid runtime dependency may be required for Core to build, run, deploy or deliver standard business functionality.**

Examples:

- AG Grid Community may be evaluated.
- AG Grid Enterprise is forbidden.
- MUI premium features are forbidden.
- paid hosted visual-regression services are not required.
- Playwright local/CI screenshots are preferred.

Commercial features require a future ADR explicitly changing this rule.

---

# 7. Mandatory Governance Migration Before Implementation

The current engineering baseline still contains older mandatory frontend decisions such as MUI/MUI X. This contract intentionally supersedes those frontend foundation choices after governance reconciliation.

The agent MUST:

1. inspect canonical `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`;
2. create a frontend foundation ADR;
3. update frontend-specific sections of `02` so one canonical answer remains;
4. preserve unrelated backend/infrastructure content;
5. update scoped frontend `AGENTS.md` only where needed;
6. update architecture/lint tests;
7. search for obsolete mandatory MUI/MUI-X requirements;
8. report unresolved conflict before implementation.

Suggested ADR:

```text
docs/adr/ADR_FRONTEND_PLATFORM_FOUNDATION_2026_10.md
```

ADR must record:

- React SPA + Vite remains;
- TypeScript strict remains;
- generated OpenAPI client remains;
- TanStack Query remains;
- React Hook Form + Zod remain for R1a unless a later ADR proves otherwise;
- MUI/MUI X stop being mandatory foundation dependencies;
- primitive/grid selection uses required spikes;
- free-only dependency rule;
- brand-agnostic package naming;
- Theme Engine/token architecture;
- backend-authoritative business calculations.

Do not leave contradictory canonical documents.

---

# 8. Requirement Classes

Every decision belongs to one class:

- **LOCKED** — architecture decision; implement unless ADR changes it.
- **SPIKE BEFORE DECISION** — prototype + objective gate required.
- **BUILD NOW** — required for R1a.
- **BUILD LATER** — seam now, full implementation deferred.
- **FORBIDDEN** — must not be introduced.

---

# 9. Locked Frontend Baseline

```text
React
TypeScript strict
Vite SPA
pnpm workspace
React Router
TanStack Query
React Hook Form
Zod
i18next + react-i18next
Vitest
React Testing Library
Playwright
Storybook
Generated OpenAPI client
Tailwind CSS v4 for design-system implementation
```

Use stable supported releases, pin exact versions, no beta/RC in production without ADR.

---

# 10. SPA Decision — LOCKED

Internal authenticated ERP = React SPA using Vite.

Do NOT introduce Next.js/SSR into the authenticated ERP.

Reasons:

- no SEO requirement;
- highly interactive long-lived app;
- separate ASP.NET Core API;
- simpler deployment/runtime/caching;
- lower solo-developer operational complexity.

Future public marketing/SEO portals may decide separately.

---

# 11. Frontend Repository Target

```text
src/frontend/
├── package.json
├── tsconfig.json
├── vite.config.ts
├── index.html
└── src/
    ├── main.tsx
    ├── app/
    ├── platform/
    ├── design-system/
    └── modules/
```

TODIR currently has one frontend application. Logical ownership boundaries are enforced inside `src/frontend/src/` and do not require separate npm packages. Extract a package only when independent deployment, independent versioning, proven cross-application reuse, or a build/tooling need earns the boundary.

---

# 12. Application Structure

```text
src/frontend/src/
├── app/
│   ├── bootstrap/
│   ├── providers/
│   ├── router/
│   └── shell/
├── platform/
│   ├── runtime/
│   ├── capabilities/
│   ├── permissions/
│   ├── entity-context/
│   ├── entity-ref/
│   ├── api/
│   ├── errors/
│   ├── notifications/
│   ├── navigation/
│   └── preferences/
├── design-system/
│   ├── tokens/
│   ├── theme/
│   └── grid/
└── modules/
    ├── directory/
    ├── catalog/
    └── ...
```

---

# 13. Standard Frontend Module

```text
modules/<module-id>/
├── api/
├── routes/
├── pages/
├── features/
├── components/
├── queries/
├── mutations/
├── schemas/
├── permissions/
├── i18n/
├── registry/
├── tests/
└── public.ts        # only when deliberate public UI surface exists
```

Create only plan-relevant folders.

---

# 14. Frontend Module Boundary — LOCKED

A module MUST NOT import another module's internal files.

Forbidden:

```ts
import { WarehousePicker } from "@/modules/inventory/components/WarehousePicker";
```

Allowed only through explicit public contract/registry.

Ownership examples:

```text
PartyPicker       → Directory
ProductPicker     → Catalog
EmployeePicker    → HR
WarehousePicker   → Inventory
```

Optional-module pickers MUST NOT live in global shared UI.

---

# 15. Shared UI vs Optional Module UI

Rule:

> If consuming a UI concept creates a hidden hard dependency, it does not belong in global shared UI.

Foundation/shared concepts may expose stable contracts. Optional module concepts stay module-owned.

---

# 16. Module UI Registry — LOCKED CONTRACT / LIMITED BUILD NOW

Provide controlled registries for approved cross-module contributions:

- entity route resolver;
- optional picker provider;
- record tab contribution;
- workspace tile;
- navigation item;
- command/action contribution.

Registration occurs only when module is active/loaded.

Do NOT build arbitrary tenant plugin runtime. No downloaded tenant JavaScript.

---

# 17. EntityRef — LOCKED

```ts
type EntityRef = {
  module: string;
  entityType: string;
  id: string;
};
```

Resolver may provide:

```ts
type ResolvedEntityRef = {
  label: string;
  secondaryLabel?: string;
  iconKey?: string;
  route?: string;
  canOpen: boolean;
};
```

Cross-module links use EntityRef/public contracts, not implementation imports.

---

# 18. Runtime Context — LOCKED

One authoritative frontend runtime context is built from backend-approved bootstrap/session data:

```ts
type RuntimeContext = {
  tenant: TenantContext;
  legalEntity: LegalEntityContext;
  branch?: BranchContext;
  user: UserContext;
  locale: LocaleContext;
  capabilities: CapabilitySet;
  permissions: PermissionSet;
  preferences: UserPreferences;
  brand: BrandProfile;
  theme: ResolvedTheme;
};
```

Frontend context improves UX; it never replaces backend authorization.

---

# 19. Tenant Context

Tenant-bound requests use central API infrastructure.

Do not let components manually compose tenant headers.

Tenant switch must:

- invalidate scoped query caches;
- clear stale module data;
- never mix previous tenant data;
- be covered by cross-tenant tests.

---

# 20. Legal Entity Context

Shell supports Legal Entity switcher when user has access to multiple entities.

Changing entity must:

- refresh effective capabilities/permissions where scoped;
- invalidate relevant query caches;
- update entity defaults;
- never silently carry incompatible editable drafts.

Entity context must be visible enough that user knows where they are working.

---

# 21. Branch Context

Branch is optional and domain-sensitive.

It may be:

- workspace context;
- transaction field;
- derived default;
- not applicable.

Do not make Branch a hidden mutable global that changes document meaning after creation.

---

# 22. Capability-Aware UI — LOCKED

UI composition derives from effective capabilities.

Do NOT scatter raw checks in 150 components.

Provide standard states/patterns:

```text
Available
Unavailable
ReadOnly
NotActivated
TemporarilyUnavailable
```

Example:

```text
Sales + Inventory active  → inventory-backed fulfillment UX
Sales without Inventory   → approved manual fulfillment UX
```

Do not show dead controls that only fail after click.

---

# 23. Module Deactivation / Read-Only UX — LOCKED

Deactivation preserves history.

Standard read-only mode:

- persistent banner;
- no create;
- no state-changing actions;
- historical data readable;
- permitted export remains;
- cross-module links remain resolvable where authorized;
- clear reason why editing is unavailable.

Do not implement read-only as random disabled controls.

---

# 24. Permissions — LOCKED

Backend is authoritative.

Frontend permission projection controls UX only:

- action visibility;
- route access;
- field presentation;
- sensitive-data presentation.

Hidden button is never security.

---

# 25. Field-Level Security — LOCKED

Sensitive values may be absent from API response.

Examples:

- cost;
- margin;
- salary;
- bank data;
- sensitive HR data.

Components must tolerate:

- absent field;
- redacted field;
- not applicable;
- read-only.

Never rely on CSS hiding sensitive values.

---

# 26. Role = Experience — LOCKED PRINCIPLE

Experience derives from:

```text
Capabilities
+ Permissions
+ Entity Scope
+ Saved Views
+ Layout Metadata
+ User Preferences
```

Do not create separate duplicate pages per role unless business workflow is genuinely different.

---

# 27. Navigation Architecture

Navigation is capability-aware and permission-aware.

Modules contribute navigation descriptors through approved registration.

Descriptor includes:

- route;
- translation key;
- icon key;
- required capability;
- required permission;
- ordering/group.

Navigation does not define backend authorization.

---

# 28. Application Shell — BUILD NOW

R1a shell includes:

- brand area;
- primary navigation;
- Legal Entity context;
- optional Branch context;
- global search seam;
- command palette seam;
- user/preferences menu;
- appearance control where allowed;
- notification surface;
- breadcrumbs/context;
- responsive behavior.

Do not overbuild dashboards before real business slices.

---

# 29. BrandProfile — LOCKED

```ts
type BrandProfile = {
  productDisplayName: string;
  logoLightRef?: string;
  logoDarkRef?: string;
  faviconRef?: string;
  primaryBrandColor?: string;
  accentBrandColor?: string;
};
```

Do not import product-name constants throughout components.

---

# 30. Theme Architecture — LOCKED

Theme is separate concerns:

```text
Theme Preset
+ Brand Profile
+ Appearance
+ Density
+ User Preference
```

Do not collapse them into one theme name.

---

# 31. ThemeDefinition — LOCKED

```ts
type ThemeDefinition = {
  preset: string;
  brand: {
    primary: string;
    accent?: string;
    neutralTone?: string;
  };
  typography?: {
    familyKey?: string;
  };
  shape?: {
    radiusPreset?: "sharp" | "standard" | "soft";
  };
  defaultDensity?: "comfortable" | "compact";
};
```

No arbitrary CSS field.

---

# 32. Theme Compiler — LOCKED

```text
ThemeDefinition
        ↓
Validation
        ↓
Color/contrast derivation
        ↓
Semantic Tokens
        ↓
Component Tokens
        ↓
CSS Variables
        ↓
Application
```

Components never know which company theme is active.

---

# 33. Color Model

Use perceptual derivation (prefer OKLCH) unless spike evidence proves safer alternative.

Requirements:

- light/dark derivation;
- semantic meaning preserved;
- contrast validation;
- unsafe values constrained;
- user gets feedback if brand color cannot produce accessible combinations;
- success/danger semantics remain stable.

No naive RGB lighten/darken-only engine.

---

# 34. Token Hierarchy — LOCKED

## Foundation/reference tokens

Raw scales: colors, typography, spacing, radius, shadows, motion, breakpoints, z-index.

## Semantic tokens

```text
surface.page
surface.card
surface.elevated
content.primary
content.secondary
content.muted
content.inverse
border.default
border.strong
border.focus
action.primary
action.primaryHover
action.secondary
status.success
status.warning
status.danger
status.info
```

## Component tokens

```text
button.primary.background
input.border
grid.header.background
dialog.shadow
sidebar.background
```

Business modules consume semantic/component APIs, never raw palettes.

---

# 35. No Hard-Coded Brand Colors

Forbidden semantic styling in modules:

```tsx
className="bg-blue-600 text-zinc-700"
```

Theme-sensitive style uses approved tokens.

Layout/mechanical utility classes may be used where they carry no product meaning.

---

# 36. Tenant Theme Freedom With Guardrails

Tenant may choose brand/accent colors.

Tenant may NOT redefine:

- danger as green;
- success as red;
- focus indication away;
- inaccessible contrast;
- arbitrary component CSS;
- arbitrary font upload;
- transaction UX semantics.

Theme changes identity, not ERP behavior.

---

# 37. Typography

Use approved font-family list tested for:

- Arabic;
- English;
- mixed text;
- financial/numeric readability;
- tabular values;
- browser performance/license.

Do not allow arbitrary tenant font binaries in R1a.

---

# 38. Numeral System Preference

Arabic locale does not force Arabic-Indic digits.

Support user preference:

```text
Latin:        123456
Arabic-Indic: ١٢٣٤٥٦
```

Business truth stays locale-neutral.

---

# 39. Appearance

R1a:

- Light required.
- Dark supported if bounded naturally by token implementation.

Architecture must allow high contrast later.

Do not build full theme editor in R1a.

---

# 40. Density

At minimum:

```text
Comfortable
Compact
```

Density independent of theme/appearance.

Controls field height, row height, toolbar height and spacing, without breaking touch usability.

---

# 41. RTL — NON-NEGOTIABLE FROM DAY 1

Arabic/RTL is not a later cosmetic feature.

Use:

- `dir` correctly;
- CSS logical properties;
- `margin-inline-*`;
- `padding-inline-*`;
- `inset-inline-*`;
- start/end terminology in component APIs.

Avoid physical left/right assumptions unless direction-independent by nature.

Every critical primitive and page slice must be tested in RTL.

---

# 42. Mixed-Direction Content

ERP screens commonly contain Arabic labels with English SKUs, emails, phones, serials, tracking IDs and money.

Components must handle bidi deliberately.

Codes/IDs may use explicit `dir="ltr"` where appropriate.

Do not force all content to page direction.

---

# 43. Accessibility Baseline

Target WCAG-aligned interaction.

Requirements:

- keyboard navigation;
- visible focus;
- correct labels;
- semantic HTML;
- screen-reader names;
- accessible dialogs;
- focus restoration;
- usable touch targets;
- contrast;
- reduced-motion consideration;
- zoom resilience.

Automated checks help but do not prove full accessibility.

---

# 44. Primitive Library Spike — REQUIRED

Evaluate only:

- React Aria Components / React Aria foundations;
- Base UI.

No third candidate without documented reason.

Equivalent spike scenarios:

1. Button.
2. Dialog.
3. Select.
4. async Combobox.
5. DateField.
6. basic numeric field.
7. Menu.
8. Tooltip/Popover.
9. RTL shell.
10. focus restoration.
11. keyboard interaction.
12. screen-reader labeling.

---

# 45. Primitive Spike Arabic/RTL Cases

Test:

- Arabic labels;
- English values in Arabic UI;
- mixed bidi strings;
- keyboard navigation RTL;
- date entry RTL;
- Arabic-Indic numerals;
- Latin numerals;
- long Arabic labels;
- portal overlays;
- validation messages.

---

# 46. Primitive Decision Gate

Prefer React Aria if required scenarios pass without forcing exact financial values through unsafe JavaScript `number` ownership.

Use Base UI if React Aria creates unacceptable constraints and Base UI passes equivalent requirements.

Scoring:

```text
Accessibility correctness        25%
Arabic/RTL behavior              20%
TypeScript/API ergonomics        15%
Exact-domain-value compatibility 15%
Composition/styling control      10%
Bundle/runtime cost               5%
Testing quality                   5%
Maintenance/community             5%
```

Record result in ADR.

---

# 47. Money Is NOT a Generic Number Input — LOCKED

No module may use a generic numeric input as authoritative Money input.

Forbidden for financial truth:

```ts
number
parseFloat(...)
Number(...)
```

Money must preserve exact decimal representation across frontend/API boundary.

---

# 48. DecimalString Contract

Concept:

```ts
declare const decimalBrand: unique symbol;

type DecimalString = string & {
  readonly [decimalBrand]: true;
};
```

External strings require runtime validation/narrowing at boundaries.

Type branding alone is not validation.

---

# 49. MoneyValue Contract

Concept:

```ts
type MoneyValue = {
  amount: DecimalString;
  currencyCode: CurrencyCode;
};
```

Generated API DTO remains authoritative transport shape.

Frontend adapters may map to ergonomic branded types without changing precision.

---

# 50. Precision Taxonomy — LOCKED

Never use one global decimal precision.

Distinguish:

- Currency amount precision.
- Unit price precision.
- Cost precision.
- Quantity/UOM precision.
- Percentage/rate precision.
- FX rate precision.
- Tax rate precision.

Example:

```text
Invoice total  → currency precision
Unit price     → may require more decimals
Quantity Kg    → UOM precision
FX rate        → separate high precision
```

---

# 51. MoneyField — BUILD NOW

MoneyField centrally owns:

- exact string editing;
- locale formatting;
- decimal/group separators;
- Arabic/Latin numeral input;
- mixed paste normalization;
- negative policy;
- precision policy;
- empty vs zero;
- read-only;
- disabled;
- locked/finalized;
- validation;
- currency display policy;
- accessibility;
- RTL behavior.

Modules MUST NOT reimplement these rules.

---

# 52. MoneyDisplay — BUILD NOW

Separate display from editor.

Requirements:

- no floating conversion;
- localized formatting;
- optional accounting negative style;
- tabular numbers where useful;
- currency symbol/code policy;
- sensible screen-reader output;
- copyable canonical value where appropriate.

---

# 53. CurrencyField / CurrencyDisplay

Currency metadata comes from FinBase/public API contract.

Never hard-code global currency list in component.

Support:

- active currencies;
- code;
- symbol;
- localized name;
- precision;
- legal-entity defaults/scope where relevant.

---

# 54. QuantityValue — LOCKED

```ts
type QuantityValue = {
  amount: DecimalString;
  uomId: string;
};
```

Quantity and UOM must not become ambiguous.

---

# 55. QuantityField — BUILD NOW

Central behavior:

- exact decimal string;
- UOM;
- precision;
- min/max only when domain contract supplies;
- conversion context;
- keyboard input;
- RTL/localized formatting.

---

# 56. Percentage / Rate Contract

Representation must be explicitly documented in API/ADR.

Never let modules decide whether `0.15` means `0.15%` or `15%`.

Frontend representation MUST match backend contract, with explicit adapter if needed.

---

# 57. LocalDate vs Instant vs Zoned DateTime — LOCKED

Do not use JS `Date` indiscriminately.

Examples:

```text
LocalDate:
- Invoice Date
- Due Date
- Payroll Date
- Accounting Period Date

Instant:
- Audit CreatedAt
- Provider event timestamp
- Payment received timestamp

Zoned DateTime:
- operational appointment where local zone matters
```

---

# 58. Date UI

Date fields must:

- support locale display/input;
- preserve date-only semantics;
- avoid timezone shifts;
- support Arabic/English;
- respect calendar policy;
- remain keyboard-friendly.

Alternate display calendars may be supported where approved, but canonical semantics remain explicit.

---

# 59. Initial Domain Field System — BUILD NOW

Central shared baseline:

```text
MoneyField / MoneyDisplay
CurrencyField / CurrencyDisplay
QuantityField / QuantityDisplay
UomField / UomDisplay
PercentageField / PercentageDisplay
LocalDateField / LocalDateDisplay
DateTimeDisplay
PhoneField
EmailField
StatusBadge / StatusDisplay
EntityRefLink
```

Module-owned pickers remain with their domains.

---

# 60. Custom Fields Boundary

Future Studio may render approved custom field types through registry.

A custom Money field is informational unless owner domain explicitly consumes it.

Custom fields do NOT automatically participate in:

- invoice totals;
- tax;
- posting;
- ledger;
- settlement;
- stock valuation;
- payroll calculations.

Allowed Studio types must be a closed list.

---

# 61. Field Registry — CONTRACT NOW / FULL STUDIO LATER

Typed registry may map semantic field type to:

- renderer;
- editor;
- grid renderer;
- optional grid editor;
- presentation metadata.

Do NOT build a generic low-code framework in R1a.

Use registry only where it reduces proven duplication.

---

# 62. Form Architecture

Keep React Hook Form + Zod for R1a unless a blocking incompatibility is proven.

Rules:

- backend validation authoritative;
- frontend schema improves UX;
- generated DTO != form state;
- explicit mapper from form state to command request;
- server errors map via stable error codes.

---

# 63. FormField Pattern

Standard composition:

```text
Label
Editor
Description/help
Validation/error
Required indicator
Read-only/locked explanation where needed
```

Do not duplicate label/error spacing across modules.

---

# 64. Field States

Standard states:

```text
Default
Hover
Focus
Disabled
ReadOnly
Locked
Error
Warning
Loading
Unavailable
```

ReadOnly != Disabled.

Locked may represent immutable business state and should explain why.

---

# 65. Business Document Lock Pattern — LOCKED

Posted/finalized immutable documents must not appear editable.

Standard UX:

- visible locked state;
- read-only fields;
- correction actions instead of edit;
- reason/explanation;
- history/audit access.

Correction actions may include Credit Note, Debit Note, Reverse, Adjustment only when owner domain exposes them.

---

# 66. Approval Pattern — BUILD NOW CONTRACT

UI supports approval guard outcomes:

```text
Allow
Deny
RequireApproval
```

When approval required:

- show pending approval;
- show allowed next actions;
- prevent bypass;
- show decision outcome/history where authorized.

Frontend never owns approval rule logic.

---

# 67. Compliance State Pattern

Legal/financial documents may expose regulatory state separately:

```text
NotRequired
PendingSubmission
Submitted
Accepted
Rejected
FailedRetryable
FailedTerminal
```

Do not overload document lifecycle status.

---

# 68. Multiple State Dimensions

A document may independently have:

- lifecycle;
- accounting;
- regulatory;
- settlement;
- fulfillment;
- approval.

Avoid one giant `status` badge.

---

# 69. Status Color Semantics

States map to semantic intents:

```text
neutral
info
success
warning
danger
```

Modules map state→intent, not state→arbitrary color.

Never encode truth by color alone.

---

# 70. Action Hierarchy

Use:

```text
Primary
Secondary
Tertiary
Ghost
Danger
```

Prefer one dominant primary action per context.

---

# 71. Confirmation Risk Levels

```text
L0 immediate safe
L1 undo/recoverable feedback
L2 confirmation
L3 destructive confirmation + reason
L4 approval/controlled irreversible workflow
```

Avoid confirmation fatigue.

---

# 72. Toast / Notification Rule

Toasts are transient feedback, NOT the only surface for:

- validation failure;
- posting failure;
- permission denial requiring explanation;
- data conflict;
- long-running job failure.

Critical failure must remain discoverable.

---

# 73. Standard Error Taxonomy

```text
FieldValidation
FormValidation
BusinessConflict
ConcurrencyConflict
PermissionDenied
CapabilityUnavailable
NotFound
NetworkUnavailable
TimeoutUnknown
RetryableServerError
NonRetryableServerError
UnexpectedError
```

Behavior uses stable backend error codes, never parsed English strings.

---

# 74. Correlation ID

Safe support/reference ID may be shown.

Never expose stack traces, SQL, secrets or sensitive PII.

---

# 75. API Client — LOCKED

ASP.NET Core OpenAPI is source of frontend transport contracts.

CI must:

- generate OpenAPI;
- detect unexpected breaking change;
- generate frontend client/types;
- detect generated-client drift.

Do not manually duplicate API DTOs.

---

# 76. Generated Client Isolation

Use thin module API layer:

```text
Generated client
    ↓
Module API adapter
    ↓
Query/Mutation hooks
    ↓
Feature/UI
```

Adapters map transport ergonomics, not business truth.

---

# 77. TanStack Query — LOCKED

Server state belongs in TanStack Query.

Do not copy server entities into Zustand.

Query keys include relevant scope:

```text
tenant
legal entity
module
entity/list
filter/pagination
branch when semantically required
```

---

# 78. Cache Invalidation

Each mutation defines explicit invalidation/update behavior.

Avoid broad global invalidation.

Entity/tenant switch must never retain stale scoped data.

Optimistic updates only where correctness risk is low and rollback is clear.

Financial posting/finalization is generally NOT optimistic.

---

# 79. Mutations and Idempotency

Critical mutations propagate `Idempotency-Key` when backend requires/supports it.

Frontend infrastructure manages stable key for the same logical retry.

Do not regenerate key on automatic retry of same operation.

Do not reuse same key for different payload.

---

# 80. Concurrency

Editable resources honor backend optimistic concurrency via ETag/version/row-version equivalent.

On conflict:

> Never silently overwrite.

Show refresh/review/recovery.

---

# 81. Request Context

Tenant/entity/correlation/idempotency concerns are injected centrally.

Business components MUST NOT manually add low-level headers.

---

# 82. Long-Running Operations

Imports/exports/generation jobs use:

- operation/job reference;
- progress/status endpoint;
- polling initially;
- inspectable failure;
- backend-defined retry/recovery.

SignalR only when justified later.

Browser never connects to RabbitMQ.

---

# 83. Page Composition Philosophy

Use reusable page archetypes for recurring ERP structures.

Do NOT build universal JSON page engine.

```text
Metadata → repetitive presentation/configuration
React composition → complex business workflows
```

---

# 84. R1a Page Archetypes

Build only:

```text
ListPage
RecordPage
DocumentPage
SettingsPage
WizardPage
DashboardShell (lightweight)
```

Do not prebuild WorkQueue/SplitView/Task frameworks until a real consumer proves need.

---

# 85. ListPage

Standard capabilities:

- title/context;
- primary action;
- search;
- filters;
- saved-view seam;
- grid/list;
- selection;
- bulk actions;
- pagination;
- import/export where permitted;
- empty/loading/error;
- read-only/capability handling.

---

# 86. RecordPage

Standard structure:

```text
RecordHeader
Summary
Actions
Tabs/Sections
Related data
Activity/History
Audit link/surface
```

Do not force every record into identical tabs.

---

# 87. DocumentPage

R1a supports:

```text
Document identity
Party/context
Lifecycle state
Approval state
Accounting state
Compliance state
Settlement/Fulfillment projection where relevant
Read-only/lock banner
Line editor/grid slot
Totals slot
Terms/details
Attachments slot
History/activity
Correction actions
Primary action area
```

DocumentPage does NOT own business rules.

---

# 88. SettingsPage

Consistent:

- sections;
- save/revert;
- dirty-state handling;
- validation;
- permissions;
- scope indicator;
- inheritance/effective setting explanation where relevant.

---

# 89. WizardPage — BUILD NOW

Mandatory for migration/import.

```text
Upload
→ Mapping
→ Validation
→ Preview
→ Dry Run
→ Execute
→ Reconcile/Result
```

Must support clear current step, error summary, backend job state and safe destructive transitions.

---

# 90. Dashboard

R1a provides lightweight shell/layout only.

Do not build generic BI framework.

Widgets use owner-module read models.

---

# 91. Data Grid Is Product-Critical

Requirements:

- server pagination;
- server sort/filter;
- column visibility;
- resize;
- pin/freeze where free candidate supports;
- row selection;
- bulk actions;
- keyboard navigation;
- accessibility;
- RTL;
- virtualization;
- custom cell renderers;
- role-aware columns;
- loading/empty/error;
- density;
- bounded editing/copy behavior.

Never load entire ledger/stock history into browser memory.

---

# 92. Grid Spike — REQUIRED

Evaluate:

1. AG Grid Community only.
2. TanStack Table + TanStack Virtual + minimal custom shell.

Enterprise/commercial modules excluded.

---

# 93. Grid Spike Scenarios

Test:

1. Arabic RTL.
2. 100k logical records through server paging/mock API.
3. sort/filter.
4. pin/freeze equivalent if free candidate supports.
5. selection.
6. bulk action.
7. Money cell.
8. Quantity/UOM cell.
9. keyboard navigation.
10. one safe inline-edit field.
11. baseline copy/paste.
12. column visibility.
13. compact/comfortable.
14. dynamic theme switch.
15. missing sensitive column.
16. permission actions.
17. responsive behavior.
18. performance profile.

---

# 94. Grid Decision Gate

Choose AG Grid Community if it satisfies R1a without Enterprise imports/license dependence.

Choose TanStack/custom only if Community cannot satisfy critical requirements and custom scope remains bounded.

```text
Free feature coverage    30%
Keyboard/ERP ergonomics  20%
RTL/accessibility        15%
Theming integration      10%
Performance              10%
Maintenance effort       10%
Bundle/runtime cost       5%
```

Record result in ADR.

---

# 95. Grid Adapter Boundary

Business modules consume platform `DataGrid` contract.

Modules MUST NOT import vendor grid APIs directly.

---

# 96. Excel-Like Scope Rule

Do not promise Excel parity.

Advanced range selection, pivoting, formulas and complex clipboard transforms are BUILD LATER unless a real workflow requires them.

---

# 97. Keyboard System — BUILD NOW FOUNDATION

Central conventions may include:

```text
Ctrl/Cmd + K      command palette
Ctrl/Cmd + S      save draft where valid
Ctrl/Cmd + Enter  primary commit where safe
Esc               close transient surface
Enter/Tab         field navigation
Arrow keys        grid navigation
```

Never bind irreversible financial action to unsafe global shortcut.

---

# 98. Command Palette — CONTRACT NOW / LIMITED BUILD NOW

Initial capabilities:

- navigate;
- create common permitted records;
- search commands;
- permission-filtered actions.

No AI autonomous command execution in this baseline.

---

# 99. Copy/Paste

Grid/form paste must:

- validate;
- normalize locale;
- reject ambiguous financial values;
- respect UOM/precision;
- surface row/cell errors.

Never blindly paste strings into business objects.

---

# 100. Search Input Pattern

Differentiate:

- local UI filtering;
- server query;
- global entity search.

Debounce only where appropriate.

Search normalization must not mutate source values.

---

# 101. Async Picker Pattern

Large pickers use server search with:

- debounce;
- stale-response handling;
- pagination;
- loading/no results;
- keyboard navigation;
- create-new only if owner module allows;
- stable selected label/reference.

Never download all Parties/Products for a select.

---

# 102. PartyPicker Ownership

Directory owns PartyPicker public contract.

Consumers may request context such as customer/supplier role or entity visibility.

---

# 103. ProductPicker Ownership

Catalog owns ProductPicker.

Price, stock and tax remain separate owner-domain enrichments.

---

# 104. Empty States

Standard EmptyState includes:

- title;
- explanation;
- approved primary action;
- optional secondary action.

Avoid generic `No data` only.

---

# 105. Loading States

Use:

- skeleton for structure;
- inline spinner for compact action;
- progress for measurable job;
- background activity indicator when interaction may continue.

Avoid fullscreen spinner for normal query refresh.

---

# 106. Responsive Strategy

```text
Desktop → full workspace
Tablet  → adaptive workspace
Mobile  → task-oriented experience
```

Desktop-first != desktop-only.

Do not shrink a dense desktop grid into mobile and call it responsive.

---

# 107. Mobile/PWA

R1a architecture must not block PWA/task workflows.

Likely mobile users:

- warehouse;
- salesperson;
- approver;
- shipping operator;
- field service.

Offline is NOT globally promised. POS/offline requires bounded design.

---

# 108. Touch

Operational/mobile UI requires:

- usable touch targets;
- no hover-only action;
- scanner-compatible focus where needed;
- safe sticky action bars where useful.

Density cannot make mobile targets unusable.

---

# 109. Printing and Legal Documents Boundary

React Design System is NOT authoritative legal PDF renderer.

Backend Print/Template Engine owns authoritative generated documents.

Frontend may preview/download.

Legal QR/fields/compliance layout is not freely themed by tenant UI settings.

---

# 110. Document Branding Scope

Application theme and legal-document branding are separate.

```text
Tenant application theme → group/product UI identity
Legal Entity A invoice   → Entity A legal identity
Legal Entity B invoice   → Entity B legal identity
```

Do not derive legal document identity solely from active UI theme.

---

# 111. i18n

Use i18next/react-i18next.

Rules:

- no hard-coded user-visible business strings;
- module translation namespaces;
- Arabic + English from first implementation;
- safe interpolation;
- pluralization tests;
- stable keys;
- English source text is not business identifier.

---

# 112. Localization vs Business Data

Localized display is not canonical data.

Stable codes remain stable:

- status code;
- currency code;
- UOM ID;
- error code.

Frontend localizes presentation.

---

# 113. Time Zone

Instant display follows approved user/entity timezone policy.

Never infer business date from browser timezone when backend semantics define another zone.

Date-only values never shift due to timezone.

---

# 114. Icons

Use one approved open-source icon set plus small owned additions if needed.

Do not mix icon libraries across modules.

Icons do not replace text where meaning is ambiguous.

---

# 115. Layout Primitives

Create only proven small primitives such as:

```text
Stack
Inline
PageSection
Toolbar
ResponsiveGrid
```

Do not rebuild CSS Flex/Grid as giant wrapper APIs.

---

# 116. Tailwind Usage

Tailwind v4 may be used inside design-system/platform implementation.

Rules:

- semantic tokens drive visual values;
- business modules avoid raw palette classes;
- arbitrary values require justification;
- repeated semantic styling belongs in shared components;
- logical properties for RTL.

---

# 117. CSS Boundary

Allowed:

- design-system styles;
- semantic CSS variables;
- module-local unique layout.

Forbidden:

- tenant custom CSS;
- global overrides of unrelated modules;
- `!important` wars;
- relying on vendor DOM internals outside adapter layer.

---

# 118. Storybook Role

Storybook is living catalog for shared foundation.

R1a required for:

- core primitives;
- domain fields;
- key page patterns;
- DataGrid wrapper;
- theme/density preview.

Do not require exhaustive stories for every one-off module component.

---

# 119. Critical Story Coverage

Representative combinations:

```text
LTR / RTL
Arabic / English
Light / Dark
Compact / Comfortable
Default / Disabled / ReadOnly / Error
Long content
Keyboard
```

Avoid useless combinatorial explosion.

---

# 120. Visual Regression

Use Playwright screenshot comparisons for critical shared surfaces.

No paid hosted service required.

Critical baselines:

- AppShell Arabic RTL.
- MoneyField.
- DocumentPage.
- DataGrid.
- theme switch.
- critical light/dark surfaces.

Run in controlled CI environment.

---

# 121. Unit / Component Tests

Use Vitest + React Testing Library.

Test behavior, not implementation details.

Priority:

- interaction;
- keyboard;
- domain normalization;
- capability/permission states;
- error mapping;
- theme resolution;
- context changes.

---

# 122. E2E

Use Playwright.

Reference flows eventually include:

```text
login/bootstrap
entity switch
Party list/create/edit
Invoice reference slice
Payment slice
Journal grid slice
Import wizard
```

---

# 123. Accessibility Automation

Use Storybook a11y/axe for critical shared components and Playwright where useful.

Automation does not replace manual keyboard/RTL review.

---

# 124. Frontend Performance Philosophy

Measure performance.

Focus on:

- route/module lazy loading;
- bounded payloads;
- virtualization;
- no giant client caches;
- no duplicate request waterfalls;
- code splitting;
- no giant side-effect barrels;
- memoization only for measured issues.

---

# 125. Lazy Module Loading — LOCKED

Optional modules lazy-load.

A tenant using three modules must not execute every future module.

Do not create import-all registry that defeats code splitting.

---

# 126. Bundle Boundary

Core shell contains platform essentials only.

Heavy module packages belong in module chunks.

Grid should not load on pages that never need it where practical.

---

# 127. Browser Storage

Do not persist sensitive business data in localStorage by default.

Allowed candidates:

- safe UI preferences;
- appearance;
- non-sensitive layout state.

Auth token strategy follows security architecture.

---

# 128. Security

Frontend must:

- prevent XSS-prone raw HTML;
- sanitize approved rich content;
- never expose/log secrets;
- never log sensitive PII;
- never trust route hiding as authorization;
- honor CSP architecture;
- avoid unsafe third-party scripts.

---

# 129. Observability

Frontend errors may include safe:

```text
correlation id
route
module
operation
release version
scoped tenant/entity identifiers where policy permits
```

No sensitive payloads.

Core architecture must not require paid error-tracking SaaS.

---

# 130. Theme Editor — BUILD LATER

R1a builds ThemeDefinition/resolver/compiler and one or two presets.

Do not build full visual Theme Editor now.

---

# 131. Theme Versioning — DESIGN NOW / BUILD WHEN NEEDED

Persistent ThemeDefinition includes schema version once customization is production-persistent.

Do not build migration engine until multiple versions exist.

---

# 132. High Contrast — DESIGN NOW / BUILD LATER

Semantic tokens must allow future high-contrast modes.

Full product support deferred unless requirement accelerates it.

---

# 133. WorkQueue / SplitView / TaskPage — BUILD LATER

Do not prebuild generic frameworks.

First real consumers define them with evidence.

---

# 134. Studio UI Runtime — BUILD LATER

Do not build Studio page generator now.

Only define field registry seams that avoid blocking future Studio.

---

# 135. Metadata Boundary

Metadata may control:

- labels;
- layout order;
- visibility;
- simple required/read-only presentation;
- custom informational fields;
- simple list columns;
- saved views.

Metadata MUST NOT own accounting/tax/payroll/stock state machines or arbitrary scripts.

---

# 136. No Generic JSON ERP Framework

Forbidden:

```text
Every page = JSON schema
Every action = dynamic metadata
Every workflow = configurable script
```

Complex workflows remain explicit React features backed by explicit domain APIs.

---

# 137. No Business Logic in Design System

Design System owns presentation/interaction/accessibility/semantic field behavior.

It does NOT decide:

- invoice posting legality;
- tax amount;
- credit availability;
- payroll formulas;
- stock truth.

---

# 138. Backend-Authoritative Calculations — LOCKED

For live document totals:

```text
User edits draft
    ↓
Debounced Preview Request
    ↓
Backend Pricing/Tax/FinBase/Owner Domain
    ↓
Authoritative Preview
    ↓
UI displays
```

Finalization recalculates/revalidates server-side.

---

# 139. Local Preview Math

Local lightweight math may only be non-authoritative UX preview using exact-decimal representation and must be replaced by backend result.

Prefer backend preview for R1a financial documents.

---

# 140. Page-Level Dirty State

Consistent dirty-state behavior.

Prompt on navigation away from meaningful unsaved changes according to risk.

Do not prompt for harmless filter/search state.

---

# 141. Draft Autosave

Not globally enabled.

Module may implement only if its business pack approves draft semantics and concurrency.

---

# 142. Saved Views

Design contract now.

May contain:

- filters;
- sort;
- visible columns;
- density where appropriate.

Saved view never bypasses field security.

---

# 143. Import / Export

Export respects:

- capabilities;
- permissions;
- field security;
- current filters;
- size bounds;
- backend async export for large data.

Never export hidden sensitive data from client cache.

---

# 144. Product Language

Frontend terminology follows canonical module packs.

Do not rename domain concepts because a UI library uses different words.

---

# 145. Routing

Routes stable, readable, module-scoped.

Concept:

```text
/<module>/<resource>
/<module>/<resource>/<id>
```

Exact tenant/entity route scope follows platform decision.

Do not encode mutable display names as IDs.

---

# 146. Route Authorization

Route guards improve UX; backend remains authoritative.

Distinguish:

- permission denied;
- module inactive/unavailable;
- not found.

Do not preload sensitive data before access decision.

---

# 147. Deep Links

Deep links survive refresh.

Context restoration verifies user access.

Never open stale entity from incompatible tenant/entity context without validation.

---

# 148. App Bootstrap

Resolve enough before module routing:

```text
auth/session
tenant
legal entity access
capabilities
permissions
brand/theme
locale/preferences
module registry/navigation
```

Prefer coherent bootstrap contract to many startup waterfalls.

---

# 149. Bootstrap Privacy

Do not render sensitive module shell then hide it after permissions load.

Use safe bootstrap loading shell.

---

# 150. Theme Test Presets

At least two contrasting dev themes:

```text
Default neutral/brand theme
Strong alternate brand theme
```

Purpose: prove no hard-coded brand color.

---

# 151. RTL Regression Gate

Representative RTL tests belong in CI.

A PR that breaks shared RTL behavior is a regression.

---

# 152. Long-Text Regression Gate

Critical layouts must be tested with:

- long Arabic company names;
- long product names;
- long translated labels;
- long validation errors;
- mixed Arabic + codes.

No silent clipping of business-critical information.

---

# 153. Reference Vertical Slices — REQUIRED

Frontend foundation MUST be validated against real business slices, not built in isolation.

Reference slices:

1. Directory Party — List + Record.
2. Invoice reference slice — Document.
3. Payment — Money/workflow/status.
4. Manual Journal reference slice — editable financial grid/keyboard.
5. Import — Wizard.

---

# 154. Why Party First

Party validates:

- ListPage;
- RecordPage;
- forms;
- async picker/search;
- address/contact;
- Arabic/English;
- permissions;
- entity visibility;
- theme;
- API/error/concurrency;
- audit.

Lower financial risk than starting with Invoice.

---

# 155. Why Invoice Reference Slice

Invoice stresses:

- DocumentPage;
- Money/Quantity;
- backend preview;
- multiple states;
- immutability;
- approval/compliance;
- correction actions;
- document-generation boundary.

Do not implement Invoicing domain before its approved implementation phase/API exists.

---

# 156. Why Journal Reference Slice

Manual Journal is grid/keyboard stress test:

- dense accountant UX;
- debit/credit exact Money;
- line editing;
- validation;
- tabular numeric display;
- keyboard navigation.

Do not make shared Design System accounting-specific.

---

# 157. Why Import Reference Slice

Import validates:

- Wizard;
- file step;
- mapping;
- validation;
- preview;
- async job;
- large error result;
- progress/reconciliation.

---

# 158. Frontend Foundation Build Order

```text
F00 Governance & repository inspection
F01 ADR + engineering baseline reconciliation
F02 TypeScript/lint/module architecture enforcement
F03 App bootstrap/runtime context
F04 Brand/Profile + token contracts
F05 Theme resolver/compiler
F06 Primitive-library spike and decision
F07 Grid spike and decision
F08 Core UI primitives
F09 Domain value/format contracts
F10 Domain fields: Money/Quantity/Date/Percentage
F11 Form system
F12 API client + Query/Mutation infrastructure
F13 App shell + entity/capability navigation
F14 Page archetypes
F15 DataGrid production wrapper
F16 Storybook/test harness
F17 Directory Party vertical slice
F18 Invoice reference foundation checks
F19 Payment reference checks
F20 Journal/Grid reference checks
F21 Import Wizard reference
F22 Security/accessibility/performance hardening
F23 Documentation/self-review/re-verification
F24 Release gate
```

Do not jump ahead unless dependency evidence justifies it.

---

# 159. F00 Exit Gate

Before code change:

- all applicable AGENTS read;
- canonical docs identified;
- frontend baseline conflicts listed;
- existing source/tests inspected;
- current dependency versions inspected;
- repository truth used instead of chat assumptions.

---

# 160. F01 Exit Gate

- ADR written.
- `02` frontend baseline reconciled.
- no contradictory mandatory MUI/MUI-X instruction remains.
- free-only rule documented.
- spike process documented.
- unrelated architecture untouched.

---

# 161. F02 Exit Gate

Automated rules enforce:

- TypeScript strict;
- no cross-module internal imports;
- no optional module import into platform/shared foundation;
- grid vendor imports restricted to grid adapter;
- primitive vendor imports restricted to UI implementation boundary after migration;
- no unapproved circular frontend dependency;
- no direct enterprise/paid grid import.

---

# 162. TypeScript Strictness

At minimum evaluate/enable compatible strict options:

```text
strict
noUncheckedIndexedAccess
exactOptionalPropertyTypes
useUnknownInCatchVariables
```

Do not weaken strictness globally to satisfy a library.

Use narrow local adapter for imperfect third-party types.

---

# 163. `any` Rule

`any` forbidden by default.

Allowed only at narrow external boundary with:

- explanation;
- immediate runtime validation/narrowing;
- no downstream leakage.

Prefer `unknown`.

---

# 164. Generated Types Rule

Do not hand-edit generated OpenAPI client/types.

Generated code may have isolated lint rules, but app code adapts via module API layer.

---

# 165. F03 Exit Gate

Runtime context handles:

- tenant;
- legal entity;
- branch where relevant;
- capabilities;
- permissions;
- locale;
- preferences;
- brand/theme.

Context-switch tests prove cache/data isolation.

---

# 166. F04 Exit Gate

- brand-neutral tokens exist;
- no product brand in reusable component API;
- semantic token set exists;
- default + alternate test theme;
- RTL logical-property baseline;
- no tenant CSS mechanism.

---

# 167. F05 Exit Gate

- ThemeDefinition validation;
- color/contrast derivation;
- safe default fallback;
- compact/comfortable separated;
- brand color updates app through tokens;
- invalid theme config fails safely.

---

# 168. F06 Primitive Spike Exit Gate

- required scenarios built;
- Arabic/RTL reviewed;
- keyboard reviewed;
- a11y checks run;
- exact Money constraint reviewed;
- score recorded;
- one candidate selected;
- abandoned spike code/dependencies cleaned;
- ADR updated.

---

# 169. F07 Grid Spike Exit Gate

- no paid/enterprise dependency;
- scenarios completed;
- performance profile recorded;
- RTL/keyboard reviewed;
- selected approach documented;
- vendor internals isolated;
- no accidental enterprise module import.

---

# 170. F08–F10 Shared UI Exit Gate

Critical components:

- stable typed API;
- Storybook high-risk states;
- keyboard behavior;
- RTL;
- accessibility baseline;
- theme/density compatibility;
- domain normalization tests;
- no business-module logic.

---

# 171. F11 Forms Exit Gate

- FormField pattern;
- backend error mapping;
- unsaved-change behavior;
- exact domain fields integrated;
- read-only/locked consistency;
- no hidden business engine inside Zod schema.

---

# 172. F12 API Exit Gate

- generated client works;
- no DTO duplication;
- query key convention;
- tenant/entity cache isolation;
- stable error model;
- idempotency support;
- concurrency support;
- correlation support;
- stale request handling.

---

# 173. F13 Shell Exit Gate

- navigation capability-aware;
- permission-aware;
- entity switch;
- RTL;
- responsive;
- alternate theme proves tokens;
- module lazy loading;
- unavailable/denied deep-link states.

---

# 174. F14 Page Archetype Exit Gate

ListPage, RecordPage, DocumentPage, SettingsPage, WizardPage exist only to level required by reference slices.

No giant prop object containing every future ERP possibility.

Prefer slots/composition and focused typed contracts.

---

# 175. F15 Grid Production Exit Gate

- server model;
- typed columns/cells;
- Money/Quantity renderers;
- permission-aware columns;
- selection/bulk actions;
- keyboard;
- RTL;
- density;
- saved-view seam;
- no full dataset loads.

---

# 176. F16 Test Harness Exit Gate

- Storybook configured;
- a11y addon/checks;
- Vitest component tests;
- Playwright configured;
- critical screenshot baselines;
- deterministic CI environment guidance;
- no paid visual service dependency.

---

# 177. Vertical Slice Exit Gate

A slice is not complete because it renders.

It must demonstrate:

- generated API;
- authorization-aware UX;
- loading/empty/error;
- RTL;
- Arabic/English;
- theme;
- responsive behavior;
- keyboard where relevant;
- concurrency where editable;
- idempotency for critical mutation;
- tests;
- no boundary violations.

---

# 178. Performance Guidance

Do not invent production SLA before measurement.

Initial expectations:

- shell does not load all modules;
- list endpoints paginate;
- search bounded/debounced;
- no N+1 client request pattern;
- route/module chunks lazy-load;
- grid responsive with realistic visible rows;
- theme switch does not reload business data;
- entity switch invalidation deterministic.

Measure in reference slices.

---

# 179. Data-Volume Test Profiles

Representative logical datasets:

```text
Party list: 100k server-side
Products:   100k+ server-side
Ledger:     millions backend-side, bounded page UI
```

Do not claim performance from 20-row fixtures only.

---

# 180. Network Resilience

Differentiate:

- offline/network failure;
- server failure;
- timeout;
- unknown outcome where backend exposes it.

Do not blindly retry dangerous mutations.

---

# 181. Retry Policy

Safe queries may use bounded retry.

Critical mutations default to no blind client retry unless idempotency contract guarantees safety.

---

# 182. Optimistic UI Policy

Allowed for low-risk preferences/simple resources.

Avoid for:

- posting;
- payment;
- stock movement;
- payroll finalization;
- approval;
- external booking with unknown outcome.

---

# 183. Accessibility vs Dense ERP

Compact density must not remove:

- visible focus;
- readable text;
- keyboard target;
- accessible semantics.

Dense accountant UI reduces wasted whitespace, not usability.

---

# 184. Arabic Dense Numeric Tables

Rules:

- consistent numeric alignment;
- tabular digits;
- deliberate bidi isolation;
- currency/UOM visible;
- no ambiguous truncation.

---

# 185. Destructive Actions

Require:

- permission/capability;
- clear label;
- confirmation level;
- reason when required;
- pending state;
- duplicate-submit prevention;
- success/failure feedback.

---

# 186. Double-Submit Protection

Shared action infrastructure supports:

- disable while pending;
- stable idempotency key where required;
- duplicate-click protection;
- no global blocking of unrelated page actions.

---

# 187. Business Action Components

Do not build generic component that owns domain rules.

Allowed:

```text
<ActionButton intent="primary" />
```

Module supplies capability/action; backend supplies legality.

---

# 188. Tables vs Grids

Do not use heavy DataGrid for every small table/list.

Use semantic table/list when complex grid behavior unnecessary.

---

# 189. Dialog vs Drawer vs Page

Guideline:

- simple confirmation → Dialog;
- short contextual edit → Drawer/Dialog;
- complex record/document → Page.

Do not nest full ERP workflows in modal chains.

---

# 190. URL State

Shareable list state may use URL:

- page;
- search;
- major filters;
- sort;
- saved view.

Never encode sensitive raw data in URL.

---

# 191. UI Preferences

Safe preferences may include:

- appearance;
- density;
- numeral style;
- locale;
- grid column layout;
- saved views.

Persist cross-device preferences server-side when justified.

---

# 192. Theme Precedence

```text
Platform defaults
    ↓
Theme preset
    ↓
Tenant brand settings
    ↓
User appearance/density
    ↓
Accessibility preference
```

Legal-document branding outside this chain.

---

# 193. Theme Failure Safety

Invalid/missing tenant theme falls back to accessible default.

Broken theme must never block login or critical ERP access.

---

# 194. Third-Party Library Encapsulation

Wrap libraries where replacement cost is material:

- grid;
- primitive interaction library;
- date adapter where domain semantics require;
- error tracking.

Do not wrap tiny stable utilities just for ceremony.

---

# 195. Dependency Additions

Before adding package, check:

- license;
- maintenance;
- bundle impact;
- TypeScript quality;
- security history;
- duplication;
- RTL/accessibility;
- whether platform/web APIs suffice.

Do not add packages for trivial helpers.

---

# 196. License Guard

CI/tooling should detect prohibited licenses where practical.

Document direct dependency licenses.

Do not introduce problematic copyleft dependency into distributed frontend without legal review.

---

# 197. No Customer Forks

Tenant customization is data/configuration.

Forbidden:

```text
if tenant == X
```

No customer-specific code branches.

Repeated need becomes supported configuration/capability.

---

# 198. Feature Flags

Feature flags are controlled rollout tools, not permanent customer-specific behavior.

Module entitlement/capability system remains business activation mechanism.

---

# 199. Development Debug UI

Must be dev-only or permission-controlled.

Never expose tokens/secrets.

Do not ship accidental mock controls to production.

---

# 200. Mocking

Stories/tests may mock API boundary.

Production runtime must never silently fall back to mock data.

---

# 201. Error Boundaries

Use React error boundaries at meaningful shell/module/page boundaries.

Broken optional module should not necessarily crash entire shell.

Unexpected-error UI includes recovery/navigation + safe correlation reference.

---

# 202. Suspense

Use only where it improves composition with selected data/router approach.

Do not add experimental complexity without need.

---

# 203. React State Philosophy

Priority:

1. server state → TanStack Query;
2. form state → React Hook Form;
3. URL state → Router/search params;
4. local UI state → React state;
5. shared client-only state → context/Zustand when justified.

No global business-entity store.

---

# 204. Zustand

Allowed only for truly shared client-only state not better represented elsewhere.

Do not store backend entities in Zustand.

---

# 205. Module Public Surface

`public.ts` is deliberate minimal contract, not re-export-everything barrel.

Architecture tests enforce minimal cross-module surface.

---

# 206. Module Route Registration

Routes register through module/runtime mechanism.

Do not scatter optional module routes throughout shell.

---

# 207. Module Navigation Registration

Navigation descriptor contains display/route/capability/permission metadata.

It does not define backend authorization.

---

# 208. Module Command Registration

Command palette contributions may register label/route/action + permission/capability.

No arbitrary tenant executable script.

---

# 209. Entity Workspace Contributions

Cross-module related tabs/panels use controlled extension points.

Example:

```text
Party Record
→ Sales summary if Sales active
→ Support cases if Helpdesk active
```

Directory must not import those implementations.

---

# 210. Customer 360 / Composite Read Models

Do not create N+1 calls across many modules.

Use approved backend composition/read API or bounded parallel queries.

Measure.

---

# 211. Accessible Status / Financial Values

Provide enough semantic text for screen readers.

Examples:

```text
Invoice status: Overdue
Total: 1,250 Egyptian pounds
```

Test dense-grid verbosity realistically.

---

# 212. Focus Management

After dialog/action/create/edit:

- move focus predictably;
- restore focus after transient surface;
- focus invalid field/summary when appropriate.

Do not strand keyboard users.

---

# 213. Validation Focus

On invalid submit:

- focus first actionable invalid field where appropriate;
- large forms may show error summary;
- never rely only on red border.

---

# 214. Phone Input

Provide reasonable international/locale UX but do not invent telecom validation system.

Do not require country flags unless product need proven.

---

# 215. Email Input

Basic syntax UX validation only.

Do not claim deliverability validation.

---

# 216. Addresses

Support country-specific structure through owner-domain contract.

Do not create one universal giant address form.

Directory owns canonical addresses; other modules use refs/snapshots according to their contracts.

---

# 217. Theme and Charts

Future charts derive palettes from theme tokens while preserving distinguishability.

Charts are not R1a foundation priority.

---

# 218. Reduced Motion

Respect `prefers-reduced-motion`.

Motion must be functional/subtle.

---

# 219. Animation

Allowed:

- short state transitions;
- dialog/drawer;
- loading feedback.

Avoid decorative long animations that slow ERP work.

---

# 220. R1a Build-Now Summary

```text
Governance migration
SPA/runtime context
Generated API boundary
TanStack Query infrastructure
Theme/tokens baseline
BrandProfile
RTL/i18n
Primitive spike + chosen primitive
Grid spike + chosen free grid
Core primitives
Money/Quantity/Percentage/Date domain fields
Forms
AppShell
Entity context
Capability/permission/read-only patterns
ListPage
RecordPage
DocumentPage
SettingsPage
WizardPage
DataGrid wrapper
Storybook critical catalog
Vitest/RTL/a11y tests
Playwright critical E2E/screenshots
Directory reference slice
```

---

# 221. Build-Later Summary

```text
Full Theme Editor
Many theme presets
Theme migration engine until needed
Full High Contrast mode unless accelerated
Generic WorkQueue framework
SplitView framework
Generic TaskPage framework
Studio page builder
Advanced spreadsheet grid features
Pivot/OLAP
Advanced BI charts
Platform-wide offline
AI UI agent framework
Cross-module dashboard builder
```

---

# 222. Forbidden Summary

```text
Brand name in reusable package/component names
Paid core dependency
Tenant custom CSS
Customer-specific code forks
Cross-module internal frontend imports
Direct vendor grid API in modules
Raw semantic palette colors in modules
Money truth as JS number
Independent browser tax/pricing/posting engines
UI-only permission security
Sensitive hidden-by-CSS data
Global Zustand business store
Full dataset loading
Generic JSON ERP page engine
Arbitrary tenant JavaScript
One overloaded document status
Custom fields entering finance accidentally
Next.js inside internal ERP without ADR
RabbitMQ browser connection
Enterprise grid modules
```

---

# 223. Agent Implementation Discipline

Agent MUST follow:

```text
DISCOVER
→ MODEL
→ PLAN
→ IMPLEMENT
→ TEST
→ REVIEW
→ FIX
→ RE-VERIFY
→ REPORT
```

Agent MUST NOT:

- generate unused component catalog;
- create speculative empty folders;
- alter backend business contracts by convenience;
- make architecture changes silently;
- claim completion because TypeScript compiles.

---

# 224. Required Phase Report

After each phase report:

- phase completed;
- files changed;
- architecture decisions;
- dependencies/licenses added;
- tests executed + exact result;
- RTL/accessibility checks;
- security/performance considerations;
- unresolved risks;
- deviations;
- next phase.

Failed exit gate = FAIL; fix before proceeding unless genuinely blocked.

---

# 225. Required Frontend Architecture Tests

CI should enforce:

```text
platform cannot import business module internals
module A cannot import module B internals
cross-module usage only through public surfaces/registries
no circular module dependency
optional module cannot become hidden platform dependency
grid vendor imports restricted to adapter
primitive vendor imports restricted to UI implementation boundary
generated API client not hand-edited
```

Use TypeScript compiler AST/custom lint where appropriate, not brittle regex.

---

# 226. Lint Rule Direction

Automate rules where possible:

- restricted imports;
- no enterprise grid packages;
- no legacy MUI imports after migration;
- no optional-module internals;
- no `any` by default;
- no sensitive console logging;
- TanStack Query lint rules;
- semantic style restrictions where enforceable.

---

# 227. Legacy MUI Migration

If MUI/MUI X code already exists:

- inventory usage;
- do not rewrite blindly;
- migrate only current required foundation/code;
- remove unused deps after verification;
- ensure no premium dependency.

If frontend is bootstrap-only, do not install MUI just because old baseline says so.

---

# 228. Source Ownership Matrix

```text
Tenant/User/Auth Context       → Platform/API
Legal Entity/Branch truth      → Platform Org contracts
Capabilities/Entitlements      → Module Registry
Permissions                    → Backend IAM; frontend projection
Brand profile                  → Tenant configuration
Theme resolution               → Frontend Theme Engine
Product identity               → Catalog
Party identity                 → Directory
Currency/Money definitions     → FinBase
Price truth                    → Pricing
Tax truth                      → Tax
Stock truth                    → Inventory
Financial document             → owner domain
Payment truth                  → Payments
Frontend visual state          → owning UI/component
```

---

# 229. Frontend Source-of-Truth Rule

Frontend caches/projections are never a second business source of truth.

Reload/reconciliation must restore backend owner truth.

---

# 230. Competitor / Market Lessons Incorporated

## Odoo

Learn:

- SPA shell;
- registries;
- reusable field/view concepts;
- extensible module frontend.

Avoid making every complex workflow a dynamic framework abstraction.

## Frappe / ERPNext

Learn:

- reusable UI foundation;
- metadata for repetitive forms/lists;
- consistent workspace.

Avoid metadata owning complex business workflows.

## SAP Fiori

Learn:

- centralized design tokens;
- semantic/component token hierarchy;
- tenant/company theming;
- role-oriented experience;
- accessibility themes.

Avoid enterprise ceremony without current need.

## Business Central / Enterprise ERP

Learn:

- role/workspace personalization;
- consistent page archetypes;
- customization without customer forks.

---

# 231. Official Research Verification Rule

Before implementing a third-party library, re-check current official docs for selected version.

Do not rely on stale chat summaries.

Record only architecture-relevant limitations.

---

# 232. Spike Uses Current Stable Versions

Primitive/grid spike uses current stable supported versions at implementation time.

Pin exact versions after selection.

No beta/RC.

---

# 233. Exact Decimal Spike

Before approving numeric primitive for Money, test:

```text
0.1
0.2
0.3
999999999999.99
0.000001
negative
Arabic-Indic input
Latin input
Arabic decimal separator
paste with grouping
empty
zero
```

Canonical value must remain exact decimal text.

Primitive that forces binary floating-point ownership cannot directly own MoneyField state.

---

# 234. Arabic Numeric Parsing

Parser explicitly defines accepted characters.

Normalize user entry to canonical decimal string.

Reject ambiguous malformed input.

Never persist localized grouping separators in API truth.

---

# 235. Financial Preview Spike

Reference Invoice flow must prove:

```text
edit quantity/unit price
→ debounce
→ backend preview
→ cancel/ignore stale request
→ latest preview wins
→ show tax/total
→ map server validation correctly
```

Stale response must never overwrite newer preview.

---

# 236. Query Race Safety

Async picker/search/preview must handle:

- request cancellation where supported;
- stale response ordering;
- rapid context change;
- entity switch.

Never show old context results.

---

# 237. Bulk Actions

Bulk actions require:

- selected count;
- eligibility;
- partial eligibility explanation;
- confirmation when needed;
- async job for large operation;
- result summary;
- no silent partial failure.

---

# 238. Permission-Aware Grid Columns

If user cannot view sensitive column:

- do not expose value in column data;
- API should omit when appropriate;
- saved view gracefully drops column;
- export follows backend permission.

---

# 239. Read Models

List/grid endpoints use compact owner-module read models.

Do not reuse giant aggregate DTO for every screen.

---

# 240. Page Data Composition

Avoid request waterfalls.

Prefer compact view models with necessary labels/refs, then deep-load details on demand.

Respect module ownership.

---

# 241. Entity Labels

EntityRef/read model should provide stable display label without N+1 API calls.

---

# 242. Cache Privacy

Never briefly show cached sensitive value while permissions/entity context changes.

Clear/re-scope caches correctly.

---

# 243. Logout

Logout clears:

- query cache;
- sensitive in-memory state;
- module runtime data;
- temporary session state.

Safe non-sensitive preferences may remain according to auth architecture.

---

# 244. Multi-Tab Behavior

R1a does not build complex collaborative multi-tab sync.

Session expiration must fail safely across tabs according to auth behavior.

---

# 245. Unsaved Draft Across Refresh

Do not persist business drafts to localStorage by default.

Server-owned drafts preferred for important documents.

---

# 246. Audit UI

AuditTrail displays backend audit evidence.

Do not reconstruct audit from local frontend events.

---

# 247. Activity vs Audit

User-friendly activity feed != immutable audit log.

Keep separate concepts.

---

# 248. Dialog Accessibility

Dialogs require:

- accessible title;
- description where useful;
- focus trap;
- initial focus strategy;
- escape behavior by risk;
- focus restoration.

Destructive confirmation should not automatically focus danger button without review.

---

# 249. Loading Button Pattern

Pending action:

- prevents duplicate submit;
- shows progress;
- retains understandable label;
- does not claim success before backend confirmation.

---

# 250. Save vs Finalize

Distinguish:

```text
Save Draft
Finalize / Post / Confirm
```

Do not label irreversible state transition as generic `Save`.

---

# 251. Breadcrumbs

Breadcrumbs reflect information hierarchy, not browser history.

---

# 252. Help Text

Use concise contextual help.

Do not put entire business manual in tooltips.

---

# 253. Localization Length

Layouts tolerate Arabic/English length differences.

Avoid fixed label widths unless justified.

---

# 254. Text Truncation

Never truncate critical financial/legal identifier/state without accessible full value.

---

# 255. Copy Buttons

Provide consistent copy interaction for authorized IDs, invoice numbers, tracking numbers and references.

---

# 256. PII Display

Mask where policy requires.

If backend should withhold data, frontend masking alone is insufficient.

---

# 257. Design-System Documentation

Shared component docs include:

- purpose;
- when to use;
- when not to use;
- API;
- accessibility notes;
- RTL notes;
- domain limitations.

Avoid bureaucracy for trivial internal helper.

---

# 258. Component Promotion Rule

Move component to shared UI only if:

- semantics truly shared;
- at least two real usages OR foundational necessity;
- API stable enough;
- no optional module internals.

Otherwise keep module-local.

---

# 259. Shared Utility Promotion Rule

Avoid `shared/utils` dumping ground.

Promote utilities only when ownership/semantics are clear.

---

# 260. Code Comments

Comments explain why/invariant/accessibility/browser/precision/concurrency decisions.

Do not comment obvious JSX mechanics.

---

# 261. Dependency Versions

Use workspace central pinning according to pnpm policy.

Do not let modules install separate React/core versions.

---

# 262. CI Frontend Gate

At minimum:

```text
install locked dependencies
license/dependency checks
lint
architecture/import checks
typecheck
unit/component tests
Storybook build
critical a11y tests
frontend build
Playwright critical E2E
critical screenshot comparisons
generated-client drift check
```

Critical architecture gates remain merge-blocking.

---

# 263. Build Warnings

Do not suppress warnings globally to get green CI.

---

# 264. Browser Support

Define supported browsers before production.

Prefer modern evergreen browsers unless customer/device evidence requires otherwise.

Warehouse/POS device constraints tested separately.

---

# 265. Progressive Enhancement

Internal ERP requires JavaScript.

Do not spend effort making full ERP work without JS.

Use semantic accessible HTML regardless.

---

# 266. Realistic Arabic Fixtures

Use realistic synthetic:

- Arabic company names;
- addresses;
- product names;
- EGP/SAR/AED;
- mixed codes;
- long legal labels.

Never real customer PII.

---

# 267. Theme Accessibility Tests

Generated brand theme checks:

- text/background contrast;
- button states;
- focus ring;
- status colors;
- disabled/read-only;
- grid selection;
- dark mode if supported.

Unsafe theme cannot publish.

---

# 268. Theme Preview

If R1a exposes brand settings, bounded preview includes:

- button;
- field;
- card;
- status;
- grid;
- sidebar/header.

No full Theme Editor needed.

---

# 269. Theme Storage

Persist ThemeDefinition, not compiled CSS.

Resolve/compile with version/hash.

---

# 270. Theme Cache

Changing theme invalidates theme cache, not business query data.

---

# 271. Theme Security

Logo/image refs use approved file service.

No arbitrary remote script/style URL.

No tenant `<style>` injection.

---

# 272. Legal Entity Document Theme

Separate from application ThemeDefinition.

UI theme cannot control invoice numbering, QR, tax fields, compliance data or mandatory legal footer.

---

# 273. Final Architecture Diagram

```text
                         ┌─────────────────────┐
                         │ ASP.NET Core APIs   │
                         │ OpenAPI / Contracts │
                         └──────────┬──────────┘
                                    │
                          generated api-client
                                    │
                     ┌──────────────▼──────────────┐
                     │ Frontend API/Data Platform  │
                     │ Query / Mutation / Context  │
                     └──────────────┬──────────────┘
                                    │
     ┌──────────────────────────────┼──────────────────────────────┐
     │                              │                              │
┌────▼─────┐                ┌───────▼────────┐            ┌────────▼───────┐
│ Runtime  │                │ Design System  │            │ Module Runtime │
│ Context  │                │ Tokens/Theme   │            │ registry/routes│
│ tenant   │                │ Primitives     │            │ capabilities   │
│ entity   │                │ Domain Fields  │            └────────┬───────┘
│ perms    │                │ Forms/Grid     │                     │
└────┬─────┘                │ Page Patterns  │                     │
     │                      └───────┬────────┘                     │
     └──────────────────────────────┼──────────────────────────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────┐
          ▼                         ▼                         ▼
      Directory                  Catalog                   Sales
          ▼                         ▼                         ▼
        ...                     Finance                   Shipping
```

---

# 274. Preferred Folder Direction

```text
src/frontend/
├── package.json
├── tsconfig.json
├── vite.config.ts
├── index.html
└── src/
    ├── main.tsx
    ├── app/
    │   ├── bootstrap/
    │   └── shell/
    ├── platform/
    │   ├── api/
    │   └── runtime/
    ├── design-system/
    │   ├── tokens/
    │   ├── theme/
    │   └── grid/
    └── modules/
```

The four major source areas are logical boundaries in one application. They are not separate npm packages today; future extraction remains possible when real reuse or deployment requirements appear.

Do not create a package solely because diagram contains it.

---

# 275. Final Technology Matrix

## LOCKED

```text
React
TypeScript strict
Vite SPA
React Router
TanStack Query
React Hook Form + Zod
i18next
Vitest + React Testing Library
Playwright
Storybook
OpenAPI-generated client
Tailwind CSS v4 for DS implementation
```

## SPIKE THEN LOCK

```text
Interaction primitives:
React Aria preferred candidate
vs Base UI

Grid:
AG Grid Community preferred candidate
vs TanStack Table + Virtual
```

## FORBIDDEN UNDER THIS BASELINE

```text
MUI as mandatory product foundation
MUI X paid/premium requirement
AG Grid Enterprise
Next.js for internal ERP
paid design-system runtime
```

Existing MUI code, if any, is migrated carefully rather than deleted blindly.

---

# 276. Final UX Invariants

1. Arabic/RTL works from first shared UI implementation.
2. Business modules do not invent separate UI language.
3. Theme changes branding, not workflow semantics.
4. Role changes experience, not business truth.
5. Permission hiding is not authorization.
6. Posted/locked state is visually unmistakable.
7. Money/Quantity/UOM meaning is never ambiguous.
8. Status meaning is never color-only.
9. Accountant workflows remain keyboard-efficient.
10. Mobile operational workflows are task-oriented.
11. Loading/error/empty states are designed.
12. Cross-module navigation does not create implementation coupling.

---

# 277. Final Technical Invariants

1. No paid core runtime dependency.
2. No cross-module internal frontend imports.
3. No business truth in local UI state.
4. OpenAPI-generated contracts are transport authority.
5. Financial exact decimals never become authoritative JS `number`.
6. Backend owns business-critical calculations.
7. TanStack Query owns server state.
8. Critical mutations honor idempotency/concurrency.
9. Tenant/entity scope centrally managed.
10. Optional modules lazy-load.
11. Theme is data/tokens, not tenant CSS.
12. Vendor-heavy components behind adapters.
13. Frontend CI enforces architecture.

---

# 278. First Implementation Command to Agent

After reading this file, execute:

```text
DISCOVER
→ GOVERNANCE RECONCILIATION
→ ADR
→ FRONTEND ARCHITECTURE TESTS/LINT
→ RUNTIME/THEME CONTRACTS
→ PRIMITIVE SPIKE
→ GRID SPIKE
→ DECISION
→ FOUNDATION IMPLEMENTATION
→ DIRECTORY VERTICAL SLICE
→ VERIFY
```

Do NOT begin by building a huge component library.

---

# 279. Recommended First PR Scope

```text
governance/ADR reconciliation
brand-neutral foundation naming
architecture/lint boundary rules
runtime context contracts
design token/theme contracts
spike scaffolding
```

Avoid Directory business UI in same PR unless reviewability remains good.

---

# 280. Recommended Second PR Scope

```text
primitive spike result
grid spike result
selected adapters
core primitive baseline
Money/Quantity/Date exact-domain proof
Storybook/test harness
```

---

# 281. Recommended Third PR Scope

```text
AppShell
API/query platform
ListPage/RecordPage
Directory Party vertical slice
RTL/Arabic/theme/accessibility/E2E
```

---

# 282. No Massive One-Shot Commit

Even if one agent executes entire contract, produce reviewable phases/commits.

Do not dump thousands of opaque files in one commit.

---

# 283. Rollback Safety

Create clean rollback point before major foundation decision.

Spike code must not leave abandoned dependencies.

---

# 284. Agent Stop Conditions

Stop affected decision and report if:

- canonical docs remain conflicting;
- dependency license violates free-only rule;
- both primitive candidates fail critical RTL/accessibility;
- free grid candidates cannot satisfy R1a without unbounded custom build;
- required backend API contract is absent/contradictory;
- locked backend/domain rule must change.

Continue unaffected safe work.

---

# 285. What Agent May Decide Independently

May decide:

- small internal file names;
- component decomposition;
- test helper structure;
- implementation details;
- token variable naming refinements;
- spike harness details;
- minor ergonomic APIs.

May NOT decide independently:

- paid dependency;
- framework replacement;
- Money representation;
- permission model;
- module ownership;
- tenant isolation;
- business state machine;
- SPA→SSR;
- tenant custom CSS/code;
- deletion of architecture tests.

---

# 286. Final Release Gate

Foundation is READY only when:

- governance conflict resolved;
- no paid core dependency;
- strict TypeScript passes;
- architecture tests pass;
- primitive decision documented;
- grid decision documented;
- theme/token architecture proven with alternate theme;
- RTL/Arabic critical surfaces pass;
- exact Money/Quantity contracts proven;
- generated API boundary works;
- tenant/entity/capability runtime works;
- page patterns validated against real slices;
- Storybook builds;
- unit/component tests pass;
- critical a11y checks pass;
- critical Playwright flows pass;
- screenshot baselines pass;
- frontend build succeeds;
- no cross-module internal imports;
- no identified sensitive-data client leak;
- canonical docs consistent.

Only then report:

> **All required tests and exit gates defined for the Frontend Platform & Design System scope pass.**

Never claim unmeasured “100% tested”.

---

# 287. Final Architect Verdict

This frontend foundation is infrastructure for every future business module, but it must not become a speculative framework project.

Approved strategy:

> **Strict contracts first, real vertical slices immediately after, shared abstractions promoted only when proven.**

The platform must provide:

- coherent UI/UX;
- tenant theming;
- brand independence;
- Arabic/RTL;
- exact domain-value editing;
- stable page composition;
- safe API/data behavior;
- capability-aware module UX;
- strict module boundaries;
- free/open-source core operation.

It must NOT create:

- brand lock-in;
- vendor lock-in;
- paid-license lock-in;
- customer forks;
- generic metadata-framework complexity;
- browser-owned business truth.

Final principle:

> **A new module should mostly compose approved building blocks and implement its unique business workflow—not reinvent fields, pages, grid behavior, theme rules, permission UX or data-access conventions.**

---

# 288. Official Research / Competitor Verification Notes

Re-check official documentation at implementation time.

Research basis used for this contract:

- Vite official documentation — SPA is a first-class/default app type.
- Tailwind CSS official documentation — theme variables/design-token support via CSS variables.
- TanStack Query official documentation — React server-state/query tooling.
- React Aria/React Spectrum official documentation — accessible/internationalized numeric/date interaction and numbering systems.
- AG Grid Community official license — Community is MIT/open source; Enterprise is separate and forbidden here.
- Storybook official documentation — component testing and accessibility checks.
- Playwright official documentation — local screenshot visual comparisons.
- Odoo official frontend framework documentation — SPA, registries, reusable field/view lessons.
- Frappe UI official documentation — reusable components and design tokens across Frappe products.
- SAP Fiori Design System — centralized design tokens, semantic/component hierarchy, theming/accessibility lessons.

Competitor behavior is evidence, not authority over our architecture.

---

# 289. Source Review Corrections Incorporated

This contract explicitly incorporates the critical review findings supplied after the initial frontend study:

- Capability-aware UI added.
- Read-only/deactivated module mode added.
- Approval/compliance/document lock patterns added.
- Field-level security added.
- Multi-entity runtime context added.
- OpenAPI/API/idempotency/concurrency layer added.
- WizardPage moved to R1a.
- Mobile/PWA and keyboard system made explicit.
- print/legal-document boundary clarified.
- Role=Experience given runtime mechanism.
- module-owned optional pickers corrected.
- custom Money fields restricted to informational behavior by default.
- EntityRef resolver added.
- optional modules lazy-loaded.
- primitive and grid choices moved behind spikes.
- backend preview endpoint made financial-authority pattern.
- precision taxonomy split.
- Theme Engine accessibility/OKLCH direction strengthened.
- numeral style moved to user preference.
- SPA/Vite locked.
- over-engineering guard added through Design Now vs Build Now and real vertical slices.

---

# 290. End

**END OF FILE — FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md**
