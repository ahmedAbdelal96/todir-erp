# ToDir ERP — Product Navigation, Access & UX Architecture Decision

**Status:** Proposed architecture decision for implementation planning; not a code change  
**Reviewed branch / commit:** `dev` / `9aa6eebf3cf86e9ce4e1ed0c565f6381ce5be25b`  
**Review scope:** Registered routes in all 24 frontend modules, App Shell, Command Palette, permission/authorization endpoints and store, module lifecycle and entitlements, platform/frontend architecture specifications, representative module-page tabs, official competitor docs.  
**Important limitation:** Static code and documentation review; no hands-on interviews, live company-specific authorization matrix, or local runtime security test performed in this review.

## 1. Executive architecture decision

**Adopt Permission-Driven Task Navigation with a shallow grouped primary navigation, independent business-task routes, contextual tabs only for a single record/workspace, and role-informed shortcuts.**

- Retain **one** common UI and shared design system for every role. Permissions and legal-entity scope determine visibility and access; personalization only changes prioritization and shortcuts.
- **Every effectively authorized, operationally available destination must be discoverable** in stable navigation or task search, including users who have only one narrow permission inside a broader module.
- **Unauthorized destinations must not appear** in navigation, command search, favorites, recents, or quick actions. Direct route access must show a controlled denied state; backend is always the authorization boundary.
- Keep module licensing/entitlements, activation/go-live, authorization scopes, and record permissions separate. A read-only module can preserve authorized historical navigation while hiding mutations.
- Never create another role/permission engine or duplicate business pages solely for different roles.

## 2. What source actually shows

### A. Routes and layout

`frontend/app/router/navigation-registry.ts` registers 24 module navigations, one entry permission each. The shell renders them in one flat sidebar list. The direct module route inventory:

| Module | Navigation permission | Route shape |
|---|---|---|
| Accounting | `ledger.account.read` | `/accounting` only |
| Banking | `banking.accounts.read` | `/banking` only |
| Catalog | `catalog.product.read` | `/catalog`, product detail |
| CRM | `crm.lead.read` | `/crm` only |
| Directory | `directory.party.read` | `/directory`, party detail |
| Expenses | `expenses.read` | `/expenses` only |
| FinBase | `finbase.currency.read` | `/finbase` only |
| Fixed Assets | `assets.read` | `/fixed-assets` only |
| HR | `hr.employee.read_basic` | `/hr` only |
| Identity | `platform.identity.read` | `/users`, user detail, domain settings |
| Inventory | `inventory.stock.read` | `/inventory` only |
| Invoicing | `invoicing.customer_invoice.read` | `/invoicing`, customer/supplier invoice list/new/edit/detail |
| Migration | `platform.migration.execute` | `/migration` only |
| Payments | `payments.read` | `/payments`, list/detail and public pay link |
| Payroll | `payroll.run.read` | `/payroll` only |
| POS | `pos.register.read` | `/pos` only |
| Pricing | `pricing.read` | `/pricing` only |
| Projects | `projects.read` | `/projects`, project workspace detail |
| Purchasing | `purchasing.po.read` | `/purchasing` only |
| Sales | `sales.quotation.read` | `/sales` only |
| Service | `service.work.read` | `/service` only |
| Settings | `platform.settings.read` | `/settings` only |
| Subledger | `subledger.read` | `/subledger` plus distinct finance pages |
| Tax | `tax.read` | `/tax` only |

**17/24** have only one operational module route; seven already demonstrate usable multi-route patterns. Existing examples of distinct routes: Invoicing, Payments, Projects, Subledger, Catalog, Directory, Identity.

Current task-level tab density in representative screens: Accounting up to 17 tab options (by mode); Inventory 11; HR 9; Payroll 9; Banking 8; CRM 5; Finance Base 6; Service substantial multi-tab workspace. Complex business operations are therefore hidden under an extra module→tab step.

### B. Confirmed access-display risks (not claimed API exploits)

1. `frontend/app/shell/app-shell.tsx::loadNavigation()`: checks **one permission for an entire module**. A user permitted only for a different child task can lose the whole module entry.
2. `loadNavigation()` on failure uses `resolveNavigation(fallbackModuleIds)`, which can show unverified/unentitled modules; must fail safely and visibly instead.
3. `permCacheRef` keys by **permission code only** and is visibly reset on logout, **not on organization switch**, risking stale navigation across legal entities/branches or role changes.
4. `GET /api/v1/authorization/permissions` (backend/Core/Hosting/PlatformHostingExtensions.cs) projects distinct codes assigned to the subject across roles; it does **not** evaluate each permission in the selected legal-entity/branch scope. Do not treat it as an effective-context authorization decision.
5. `GET /api/v1/modules` returns the static manifest registry; it is **not** a per-tenant entitlement+activation projection.
6. `frontend/app/shell/command-palette-registry.ts` creates several unconditional quick actions (new customer/supplier invoice, product, party, etc.) independently of effective permissions.
7. `frontend/modules/crm/pages/crm-page.tsx` currently has a `canManage = ... : true` fallback if optional permissions are missing; `frontend/modules/sales/pages/sales-page.tsx` also has permissive fallbacks. They are inappropriate for permission-based UI. No unauthorized backend action has been demonstrated by these observations.
8. The app entry (`frontend/main.tsx`) and router (`frontend/app/router/app-router.tsx`) do not mount the optional `RuntimeProvider` globally; `app-shell.tsx` provides an Outlet context, but some modules use `useOptionalPermissions()` rather than that context.
9. The route tree in `app-router.tsx` registers module pages without a shared permission-aware route boundary. Backends may reject API calls, but user-facing direct-route behavior is inconsistent.
10. Current favorites/recent state operates at **module key level**, not independent business destinations.

### C. Existing authoritative design contract already agrees

`docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md` §§19–28 locks:
- legal-entity switching refreshes scoped effective permissions/capabilities;
- backend is authoritative;
- role experience = capabilities + permissions + entity scope + preferences, not separate pages;
- module-provided navigation descriptors include route, translation, capability, permission, group/order;
- read-only history should remain accessible where authorized;
- use consistent app shell, breadcrumbs, search and responsive behavior.

`frontend/AGENTS.md` already provides `ListPage`, `RecordPage`, `DocumentPage`, `WizardPage`, `SettingsPage`. `.agents/skills/erp-frontend-architect/references/ux-laws.md` mandates deep-linkable workspaces and separate full routes for complex workflows. The implementation should **reconcile with existing governance**, not invent an independent design standard.

## 3. Proposed information architecture

**Primary persistent navigation (shallow categories):**

- **الرئيسية / Workspace:** task-focused home with work queue and current legal-entity scope.
- **المبيعات والعملاء:** CRM, Sales, Invoicing (customer flows), Directory, Pricing.
- **المشتريات والموردون:** Purchasing, supplier invoice actions (deep links to owning Invoicing module), relevant supplier Directory actions.
- **المخزون والتشغيل:** Catalog, Inventory, POS (direct high-speed workspace shortcut), Projects, Service.
- **المالية والخزائن:** Accounting, FinBase, Banking, Payments, Subledger, Tax, Expenses, Fixed Assets.
- **الموارد البشرية:** HR, Payroll.
- **الإدارة والإعدادات:** Identity/Users, Settings, Migration (restricted to permissioned administrators).

**Design constraint:** Do not show every descendant in an infinitely expanding tree. On entering a module, use an **optional contextual module menu** with up to roughly 5–8 clear first-level business destinations. The sidebar should be collapsible; mobile uses an accessible drawer/task sheet. One canonical owning route per business function; surface cross-functional task links without duplicating business data.

**Page versus tab rule:**
- Distinct daily business job or list = independent route, searchable and bookmarkable.
- Record/document detail = an accessible deep-linkable inspector drawer where the shared `RecordPage` contract calls for preserving list context; provide a dedicated canonical record route for genuinely complex documents/workspaces. Internal tabs should only divide summary, related transactions, notes and audit for the **same** object.
- Lightweight action = dialog/drawer; multi-stage financial operation = full-page workbench/wizard.
- Dashboards are optional module landing pages, not mandatory transit stops for expert operators.
- URLs preserve filters, page, selected record and workspace state; avoid opaque `?tab=` as the sole route to important jobs.

**Banking example:**
`/banking` (overview), `/banking/accounts`, `/banking/accounts/:id`, `/banking/statements`, `/banking/statements/:id`, `/banking/reconciliations`, `/banking/reconciliations/:id` (full-width workbench), `/banking/cash-counts`, `/banking/transfers`, `/banking/forecast`, `/banking/reports`.

**CRM example:**
`/crm` (My Work), `/crm/leads`, `/crm/leads/:id`, `/crm/opportunities`, `/crm/opportunities/:id`, `/crm/activities`, `/crm/management` (only when effectively authorized). One organization, multiple perspectives—not separate CRM products.

**Accounting example:** split entries, journals, chart of accounts, periods, reconciliation, financial statements, trial balance, general ledger into separate routes; retain the shared existing page components and respect Managed/Manual mode/capability rules. No 17-item horizontal tab strip.

## 4. One effective access contract, not a new permission system

**Effective destination decision:**

`visible destination = registered destination && user authorized for that destination/action in CURRENT scope && applicable module entitled/operational OR authorized read-only history under deactivation policy`.

Parent module/group visible iff **at least one child destination is visible**. Never demand an unrelated parent permission such as `banking.accounts.read` for a `banking.cash.count`-only operator. Do not infer permission dependencies that the existing backend authorization catalog does not define.

**Backend authorization remains authoritative** and evaluates actual role assignments with Tenant/LegalEntity/Branch scope and record policies. Navigation service is an **effective access projection**, not a new ACL table or bypass.

Recommended integration: a **single authenticated, scoped, batched effective-access bootstrap** (API shape to reconcile with existing backend models) returning current context, allowed destination permission codes or allowed registered navigation identifiers, entitlement/activation/read-only states, plus a revision/expiry policy. Do not drive the sidebar using 24+ independent `/authorization/check` calls; do not treat unscoped `/authorization/permissions` as effective. Reuse `IAuthorizationSubjectStore`, `IPermissionAuthorizer`, `IModuleRegistry`, entitlement/lifecycle services and existing guarded API handlers.

**Frontend one shared registry** should provide stable `id`, `parentId`/group, owning module, canonical path, translation keys, icon, destination kind, `anyOf`/`allOf` permission condition, required capability, order and search keywords. Evaluated access should drive: sidebar, contextual menu, route boundary, command palette, quick actions, favorites/recents, homepage shortcuts, and action affordances.

**Important states:**
- **Unauthorized:** hide link and action; direct URL renders helpful 403 state without sensitive data; backend denies.
- **Unknown auth/entitlement:** show loading or retry/error, never default allow or silently mark everything forbidden.
- **Authorized and operational:** visible and usable.
- **Authorized but read-only:** preserve permitted history; explicit banner, suppress mutations.
- **Not activated/not entitled:** operational entry restricted per existing commercial policy; any administrator activation/subscription route is separate and permissioned. Do not falsely grant use because a permission string exists.
- **Create-only permissions:** if supported by backend, provide direct create route/action even when list read is not allowed.

**Scope lifecycle:** on login/logout, tenant/context switch, branch change, permissions/role assignment change, activation/entitlement change, refresh of stale session: refetch effective access and invalidate scoped caches; clear old `permCacheRef` semantics. Browser must never show previous-entity sensitive data during transitions.

**Security invariant:** Navigation is discoverability, not security. API handlers and PostgreSQL RLS still enforce all scope and record constraints independently. Tests must cover both *lack of access* and *authorized discoverability*.

## 5. Competitor-derived product principles

- **Daftra:** role/employee rights and pages can be selectively inaccessible; practical lesson: actionable permissions and explicit scope matter, including user-specific records and branch restrictions. See https://docs.daftra.com/user_manual/صلاحيات-الموظف-والأدوار-الوظيفية/ .
- **Odoo 19:** menus/groups plus access rights and record rules; search, filter, favorites are core everyday workflows. Take modular task navigation and list filtering, not arbitrary complexity. See https://www.odoo.com/documentation/19.0/applications/general/users/access_rights.html and https://www.odoo.com/documentation/19.0/applications/general/search.html .
- **Dynamics 365 Business Central:** role centers/task-centered start pages and searchable/bookmarkable pages. Take tailored priorities while preserving stable access. See https://learn.microsoft.com/en-us/dynamics365/business-central/ui-personalization-user .

**Do not copy competitor visuals blindly.** Arabic-first wording, right-to-left task layout, localized currency and date presentation, keyboard-heavy accountants, and simplified owner overview differentiate ToDir in Egypt and GCC.

## 6. Prioritized implementation sequence (no rewrite)

**P0 — Access & safety foundation (must precede navigation rollout)**
1. Replace permissive fallback navigation and `canManage=true` / similar missing-context assumptions with deny-safe, observable states.
2. Fix scope-aware access projection and lifecycle refresh; do not rely on unscoped raw permission lists.
3. Filter command quick actions and bookmarks with the same access projection; add explicit permission-aware route boundary.
4. Add tests proving authorized child destination visible despite lack of unrelated parent permission, unauthorized destination absent, direct route denied, and entity switching invalidates access.

**P1 — Shared navigation architecture**
1. Extend existing `ModuleNavigationRegistration` compatibly to hierarchical destination descriptors.
2. Render shallow grouped sidebar and optional contextual module menu; preserve collapsed/mobile layouts, RTL, focus/keyboard accessibility.
3. Integrate breadcrumbs, stable destination keys, command search, favorites and recent destinations; preserve existing saved module preferences.
4. Preserve `?tab=...` deep links through compatibility redirects/mapping, including filters and selected records.

**P2 — Pilot two contrasting modules**
1. **Banking:** independent accounts, statements, reconciliation workbench, cash, transfers, forecast and reporting; keep completed business components/services.
2. **CRM:** My Work, leads, opportunities, activities, management; validate limited-scope operator versus manager experience.
3. Test with at least three representative roles: narrow cashier/custodian, accountant, and owner/manager (plus limited CRM salesperson).
4. Do not call ready without real-browser task completion and scoped permission testing.

**P3 — Progressive adoption**
Accounting and Inventory (highest tab pressure), then Payroll/HR, Sales/Purchasing, fixed assets, service, tax and other single-page modules. Invoicing, Payments, Subledger, Projects, Directory and Catalog already have useful independent-route structures: adapt registration and access, not recreate their pages.

## 7. Acceptance contract

**Access matrix tests** (minimum):
- No Banking permission => no Banking destinations in sidebar, command search, favorites or homepage; direct route and API reject.
- Cash count-only => cash-count destination visible and operable; no bank-account read or reconciliation entry.
- Ledger report-only => report visible without journal/config management.
- Create-only => direct creation discoverable if authorized by backend contract; unrelated list absent.
- Cross-entity: Atlas-only role never inherits permissions/data in Delta after organization switch. Switch back restores the correct menu.
- Branch-scoped and entity-scoped combinations; expired entitlement; activated/read-only states; role revoke mid-session; API outage; deep URL to forbidden destination.
- Client never displays sensitive records from a previous legal entity while refetching.
- Existing SaaS tenant/module activation entitlement checks and backend RLS remain effective.

**Task usability tests:** one inexperienced operator, one experienced accountant, one owner/manager. Given actual jobs (start reconciliation, create salary run, find a cash count, view receivables, create order), measure completion without help, wrong turns, time-to-first-action, and ability to return to the correct record. Test Arabic RTL/English LTR; desktop 1440 and 768 and mobile ~390; keyboard and focus behavior.

**Target, not already-measured results:** permission-navigation consistency 100% for enumerated tested destinations/scopes; zero links to unauthorized actions; no authorized orphan pages; user succeeds in common task without visiting unrelated module dashboards; no security regression.

## 8. Explicit non-goals

- No new RBAC/ABAC engine, separate subscription toggle per module, or new authorization store.
- No full frontend redesign, theme change, or duplication of shared business components.
- No permission based solely on CSS hiding; no UI state used as proof of backend authorization.
- No mass migration of 24 modules before validating the pilot.
- No direct modifications to populated `todir` database, migration checksums, or destructive test fixtures.

**Decision:** Implement an existing-contract-aligned **permission-safe navigation foundation first**, then shallow task pages and contextual tabs; run a Banking + CRM pilot before broad migration. This is a product-level architecture upgrade, not a screen redesign.
