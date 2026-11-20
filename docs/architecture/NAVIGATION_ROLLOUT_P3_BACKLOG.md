# TODIR ERP — Navigation & Task Destinations P3 Progressive Rollout Backlog

Following the successful implementation of P0 (Authorization & Security Foundation), P1 (Shared Grouped Navigation & Destination Architecture), and P2 (Pilots for Banking & CRM), this document defines the phased migration backlog for the remaining modules across TODIR ERP SaaS.

---

## 1. Governance & Prerequisites for Migration

Each module migrated in P3 must adhere to the contract established in:
- `docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md`
- `frontend/AGENTS.md` (Section 0.3)
- `.agents/skills/erp-frontend-architect/SKILL.md` (Step 3, Rule 6)

### Mandatory Invariants for Every Migrated Module:
1. **Destination Registration**: Declare explicit task sub-destinations in `destination-registry.ts` and module `routes.tsx` via `destinations: [...]`.
2. **Zero Single-Permission Module Gate**: Use composite `PermissionCondition` (`anyOf: [...]`) covering all sub-capabilities.
3. **Independent Routes & Deep Links**: Provide discrete path routes (e.g. `/accounting/journals`, `/accounting/trial-balance`) while supporting legacy query strings (`/accounting?tab=journals`).
4. **Deterministic Root Route Landing**: The root route of each module must never fail closed with Access Denied when a user possesses any permitted sub-destination; it renders the overview if permitted, or deterministically redirects (`<Navigate to={firstPermitted.path} replace />`) to the user's first permitted task destination.
5. **Fail-Closed Protection**: Direct navigation to forbidden task routes renders `<DefaultAccessDeniedView>` with status 403.
6. **Command Palette Integration**: Add Arabic and English search keywords so tasks are discoverable via Ctrl+K spotlight search.
7. **ReadOnly / WindDown Enforcement**: When a module lifecycle status is `ReadOnly` or `WindDown`, mutation routes and actions must be disabled/forbidden while historical reading remains operational.

---

## 2. Progressive Rollout Waves

### Wave 1: High Tab-Pressure Modules (Delivered & Verified)

#### 1. Accounting (`accounting` & `ledger`) — Category: `finance`
- **Current State**: Dedicated task routes for Overview, Journals, Ledger, Trial Balance, Statements, Periods, Reconciliation, and Revaluation.
- **Target Destinations**:
  - `accounting-overview`: `/accounting` — Chart of Accounts & Balances / دليل الحسابات والأرصدة (`ledger.account.read`, `ledger.account.manage`)
  - `accounting-journals`: `/accounting/journals` — Journal Entries & Posting / قيود اليومية والترحيل (`ledger.journal.read`, `ledger.journal.manage`, `ledger.general_journal.post`)
  - `accounting-ledger`: `/accounting/ledger` — General Ledger / دفتر الأستاذ العام (`ledger.reports.read`)
  - `accounting-trial-balance`: `/accounting/trial-balance` — Trial Balance / ميزان المراجعة (`ledger.reports.read`, `accounting.statement.read`)
  - `accounting-statements`: `/accounting/statements` — Financial Statements / القوائم المالية والختامية (`accounting.statement.read`, `accounting.statement.manage`)
  - `accounting-periods`: `/accounting/periods` — Fiscal Years & Periods / الفترات المالية والإقفال (`ledger.period.read`, `ledger.period.manage`, `accounting.close.execute`)
  - `accounting-reconciliation`: `/accounting/reconciliation` — Control Account Reconciliation / مطابقة حسابات المراقبة (`accounting.reconciliation.read`, `accounting.reconciliation.run`)
  - `accounting-revaluation`: `/accounting/revaluation` — FX Currency Revaluation / تقييم فروق العملة (`accounting.fx-revaluation.read`, `accounting.fx-revaluation.post`)

#### 2. Inventory & Stock Operations (`inventory`) — Category: `inventory`
- **Current State**: Dedicated task routes for Overview, Balances, Movements, Transfers, Counts, Adjustments, Warehouses, and Traceability.
- **Target Destinations**:
  - `inventory-overview`: `/inventory` — Stock Position & Balances / أرصدة ومواقع المخزون (`inventory.stock.read`)
  - `inventory-movements`: `/inventory/movements` — Stock Movement History / سجل حركة الأصناف المخزنية (`inventory.history.read`)
  - `inventory-transfers`: `/inventory/transfers` — Inter-Warehouse Transfers / أوامر التحويل بين المستودعات (`inventory.transfer.create`, `inventory.transfer.ship`, `inventory.transfer.receive`)
  - `inventory-counts`: `/inventory/counts` — Physical Stock Count Orders / محاضر الجرد الفعلي للمخزون (`inventory.count.create`, `inventory.count.enter`, `inventory.count.approve`, `inventory.count.post`)
  - `inventory-adjustments`: `/inventory/adjustments` — Stock Adjustments / أذون التسوية المخزنية (`inventory.adjustment.create`, `inventory.adjustment.approve`, `inventory.adjustment.post`)
  - `inventory-warehouses`: `/inventory/warehouses` — Warehouses & Storage Locations / المستودعات ومواقع التخزين (`inventory.warehouse.manage`, `inventory.location.manage`)
  - `inventory-traceability`: `/inventory/traceability` — Batch, Lot & Serial Traceability / تتبع التشغيلات والأرقام التسلسلية (`inventory.traceability.manage`)

---

### Wave 2: Commercial & Operational Modules

#### 3. Sales (`sales`) — Category: `sales`
- **Current State**: `/sales` with orders, quotations, customer returns, price lists.
- **Target Destinations**:
  - `sales-orders`: `/sales` or `/sales/orders` — Sales Orders / أوامر البيع (`sales.order.read`, `sales.order.create`, `sales.order.confirm`)
  - `sales-quotations`: `/sales/quotations` — Quotations / عروض الأسعار (`sales.quotation.read`, `sales.quotation.create`, `sales.quotation.open`, `sales.quotation.accept`)
  - `sales-returns`: `/sales/returns` — Customer Returns / مرتجعات المبيعات (`sales.return.create`, `sales.return.approve`, `sales.return.execute`)
  - `sales-deliveries`: `/sales/deliveries` — Shipping & Dispatch / الشحن والتسليم (`sales.order.fulfill`)

#### 4. Purchasing (`purchasing`) — Category: `purchasing`
- **Current State**: `/purchasing` with POs, requisitions, vendor returns.
- **Target Destinations**:
  - `purchasing-orders`: `/purchasing` or `/purchasing/orders` — Purchase Orders / أوامر الشراء (`purchasing.po.read`, `purchasing.po.create`, `purchasing.po.approve`)
  - `purchasing-requisitions`: `/purchasing/requisitions` — Purchase Requisitions / طلبات الشراء الداخلية (`purchasing.po.read`, `purchasing.po.create`)
  - `purchasing-returns`: `/purchasing/returns` — Vendor Returns / مرتجعات المشتريات (`purchasing.po.edit`, `purchasing.po.cancel`)
  - `purchasing-receipts`: `/purchasing/receipts` — Goods Receipts / استلامات المشتريات (`purchasing.receipt.view`, `purchasing.receipt.request`)

#### 5. POS & Retail (`pos`) — Category: `inventory`
- **Current State**: Cashier workspace, sessions, registers.
- **Target Destinations**:
  - `pos-register`: `/pos` — Active Register & Cashier Terminal / نقطة البيع (`pos.register.read`, `pos.cart.manage`, `pos.checkout.create`)
  - `pos-sessions`: `/pos/sessions` — Cashier Shift Sessions & Cash Drops / جلسات الورديات (`pos.session.open`, `pos.session.close`, `pos.cash.manage`)
  - `pos-orders`: `/pos/orders` — POS Receipts & Returns / إيصالات البيع والمرتجعات (`pos.return.create`, `pos.exchange.create`, `pos.report.read`)

---

### Wave 3: Workforce & Human Capital

#### 6. Human Resources (`hr`) — Category: `hr`
- **Current State**: Employees, contracts, attendance, leaves.
- **Target Destinations**:
  - `hr-employees`: `/hr` or `/hr/employees` — Employee Directory / دليل الموظفين (`hr.employee.read_basic`, `hr.employee.create`, `hr.employee.manage`)
  - `hr-contracts`: `/hr/contracts` — Employment Contracts / عقود العمل (`hr.employee.manage`, `hr.employee.import`)
  - `hr-leaves`: `/hr/leaves` — Leave Requests & Balances / الإجازات والمغادرات (`hr.employee.manage`)
  - `hr-attendance`: `/hr/attendance` — Attendance & Timesheets / الحضور والانصراف (`hr.employee.manage`)

#### 7. Payroll (`payroll`) — Category: `hr`
- **Current State**: Payroll runs, payslips, deductions.
- **Target Destinations**:
  - `payroll-runs`: `/payroll` — Monthly Payroll Runs / مسيرات الرواتب (`payroll.run.read`, `payroll.run.create`, `payroll.run.approve`, `payroll.run.finalize`)
  - `payroll-payslips`: `/payroll/payslips` — Employee Payslips / قسائم الرواتب (`payroll.payslip.read`, `payroll.payslip.read_self`, `payroll.payslip.export`)
  - `payroll-adjustments`: `/payroll/adjustments` — Allowances & Deductions / البدلات والاستقطاعات (`payroll.input.read`, `payroll.input.create`, `payroll.input.approve`, `payroll.structure.manage`)

---

### Wave 4: Specialized Financial Modules

#### 8. Fixed Assets (`fixed-assets`) — Category: `finance`
- **Target Destinations**:
  - `fixed-assets-register`: `/fixed-assets` — Asset Register / سجل الأصول الثابتة (`assets.read`, `assets.create`, `assets.edit`)
  - `fixed-assets-depreciation`: `/fixed-assets/depreciation` — Depreciation Schedules / مسيرات الإهلاك (`assets.depreciation.prepare`, `assets.depreciation.post`, `assets.depreciation.reverse`)
  - `fixed-assets-disposals`: `/fixed-assets/disposals` — Disposals & Revaluations / استبعاد وإعادة تقييم الأصول (`assets.disposal.manage`, `assets.impair`, `assets.revalue`)

#### 9. Tax & Compliance (`tax`) — Category: `finance`
- **Target Destinations**:
  - `tax-returns`: `/tax` — Tax Returns & Declarations / الإقرارات الضريبية (`tax.read`, `tax.simulate`, `tax.export`)
  - `tax-withholding`: `/tax/withholding` — Withholding Tax / ضريبة الاستقطاع (`tax.read`, `tax.policy.read`, `tax.policy.manage`)
  - `tax-settings`: `/tax/settings` — Tax Rates & Categories / فئات وقواعد الضرائب (`tax.registration.read`, `tax.registration.manage`, `tax.rule.read`, `tax.rule.manage`)

---

## 3. Rollout Acceptance Checklist

For each migrated module:
- [x] Registered in `destination-registry.ts` with explicit `NavDestination` array and keywords.
- [x] Updated module `routes.tsx` with dedicated path-based sub-routes wrapped in `<PermissionGuard>`.
- [x] Module page component accepts `initialTab` and updates URL route on tab click.
- [x] Deterministic root route landing via `ModuleRootRoute` prevents 403 on parent links for narrow roles.
- [x] Automated regression test (`permissions-manifest-audit.test.ts`) verifies 100% manifest parity.
- [x] Direct navigation to forbidden route fails closed with `<DefaultAccessDeniedView>`.
