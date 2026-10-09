# Application Layer Governance (`src/frontend/src/app/`)

## Identity and responsibility

The `app/` directory owns top-level application bootstrapping, shell layout, routing, provider orchestration, runtime context wiring, and top-level navigation.

Core principle:
> **`app/` is for composition, not business logic or domain calculations.**

---

## 1. Absolute filename prohibition

Never introduce a new file or directory whose basename contains `todir` in any casing. Use descriptive kebab-case names (e.g. `app-shell.tsx`, `nav-bar.tsx`, `breadcrumbs.tsx`).

---

## 2. Architectural rules for `app/`

1. **Top-Level Composition Only:** `app/` arranges the shell, layout, global providers (`QueryClientProvider`, `RuntimeProvider`), theme injector, and error boundaries (`ShellErrorBoundary`).
2. **Dynamic Module Lazy-Loading:** All business module pages (Directory, Invoicing, Payments, Accounting, Migration, etc.) rendered by the shell or router MUST be imported dynamically via `React.lazy()` and wrapped in `<Suspense>`. Eagerly importing business module pages into the startup bundle is strictly forbidden.
3. **Capability & Permission-Aware Navigation:** Sidebar navigation and top-level tabs must render items conditionally based on platform capabilities and user permissions.
4. **No Business Domain Logic:** `app/` must never contain invoice pricing math, ledger balance rules, tax calculations, or domain validation.
5. **No Direct Module Internals:** `app/` must import business modules only through their public entry points (`modules/<module>/pages/*` or `modules/<module>/public.ts`).
6. **No Duplicated Platform Infrastructure:** Do not create duplicate HTTP clients, query factories, or theme compilers inside `app/`; consume `platform/*` and `design-system/*`.
