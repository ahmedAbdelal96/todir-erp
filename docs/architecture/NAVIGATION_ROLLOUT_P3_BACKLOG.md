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
4. **Fail-Closed Protection**: Direct navigation to forbidden task routes renders `<DefaultAccessDeniedView>` with status 403.
5. **Command Palette Integration**: Add Arabic and English search keywords so tasks are discoverable via Ctrl+K spotlight search.

---

## 2. Progressive Rollout Waves

### Wave 1: High Tab-Pressure Modules (Immediate Next Priorities)

#### 1. Accounting (`accounting`) — Category: `finance`
- **Current State**: Single `/accounting` page with tabs for Journals, General Ledger, Trial Balance, Fiscal Periods, and Settings.
- **Target Destinations**:
  - `accounting-overview`: `/accounting` — Overview & Financial Health (`accounting.reports.read`)
  - `accounting-journals`: `/accounting/journals` — Daily Journal Entries / قيود اليومية (`accounting.journal.read`, `accounting.journal.create`, `accounting.journal.post`)
  - `accounting-ledger`: `/accounting/ledger` — General Ledger / دفتر الأستاذ العام (`accounting.ledger.read`)
  - `accounting-trial-balance`: `/accounting/trial-balance` — Trial Balance / ميزان المراجعة (`accounting.trial_balance.read`)
  - `accounting-periods`: `/accounting/periods` — Fiscal Years & Periods / الفترات المالية والإقفال (`accounting.period.manage`)
  - `accounting-closing`: `/accounting/closing` — Year-End Closing / الإقفال السنوي (`accounting.period.manage`, `accounting.year_end.close`)

#### 2. Inventory & Stock Operations (`inventory`) — Category: `inventory`
- **Current State**: Single `/inventory` page with tabs for Stock Balances, Movements, Adjustments, Transfers, and Counts.
- **Target Destinations**:
  - `inventory-overview`: `/inventory` — Stock Position / أرصدة المخزون (`inventory.stock.read`)
  - `inventory-receipts`: `/inventory/receipts` — Goods Receipt Notes / إذن استلام بضاعة (`inventory.receipt.read`, `inventory.receipt.create`)
  - `inventory-deliveries`: `/inventory/deliveries` — Delivery Orders / إذن صرف بضاعة (`inventory.delivery.read`, `inventory.delivery.create`)
  - `inventory-adjustments`: `/inventory/adjustments` — Stock Adjustments / تسويات مخزنية (`inventory.adjustment.read`, `inventory.adjustment.manage`)
  - `inventory-counts`: `/inventory/counts` — Physical Stock Count / جرد مخزني (`inventory.count.read`, `inventory.count.manage`)
  - `inventory-transfers`: `/inventory/transfers` — Warehouse Transfers / تحويلات بين المستودعات (`inventory.transfer.read`, `inventory.transfer.manage`)

---

### Wave 2: Commercial & Operational Modules

#### 3. Sales (`sales`) — Category: `sales`
- **Current State**: `/sales` with orders, quotations, customer returns, price lists.
- **Target Destinations**:
  - `sales-orders`: `/sales` or `/sales/orders` — Sales Orders / أوامر البيع (`sales.orders.read`, `sales.orders.create`)
  - `sales-quotations`: `/sales/quotations` — Quotations / عروض الأسعار (`sales.quotations.read`, `sales.quotations.create`)
  - `sales-returns`: `/sales/returns` — Customer Returns / مرتجعات المبيعات (`sales.returns.read`, `sales.returns.manage`)
  - `sales-deliveries`: `/sales/deliveries` — Shipping & Dispatch / الشحن والتسليم (`sales.delivery.read`)

#### 4. Purchasing (`purchasing`) — Category: `purchasing`
- **Current State**: `/purchasing` with POs, requisitions, vendor returns.
- **Target Destinations**:
  - `purchasing-orders`: `/purchasing` or `/purchasing/orders` — Purchase Orders / أوامر الشراء (`purchasing.orders.read`, `purchasing.orders.create`)
  - `purchasing-requisitions`: `/purchasing/requisitions` — Purchase Requisitions / طلبات الشراء الداخلية (`purchasing.requisitions.read`, `purchasing.requisitions.create`)
  - `purchasing-returns`: `/purchasing/returns` — Vendor Returns / مرتجعات المشتريات (`purchasing.returns.read`, `purchasing.returns.manage`)
  - `purchasing-receipts`: `/purchasing/receipts` — Goods Receipts / استلامات المشتريات (`purchasing.receipts.read`)

#### 5. POS & Retail (`pos`) — Category: `inventory`
- **Current State**: Cashier workspace, sessions, registers.
- **Target Destinations**:
  - `pos-register`: `/pos` — Active Register & Cashier Terminal / نقطة البيع (`pos.register.operate`)
  - `pos-sessions`: `/pos/sessions` — Cashier Shift Sessions & Cash Drops / جلسات الورديات (`pos.session.manage`)
  - `pos-orders`: `/pos/orders` — POS Receipts & Returns / إيصالات البيع والمرتجعات (`pos.orders.read`)

---

### Wave 3: Workforce & Human Capital

#### 6. Human Resources (`hr`) — Category: `hr`
- **Current State**: Employees, contracts, attendance, leaves.
- **Target Destinations**:
  - `hr-employees`: `/hr` or `/hr/employees` — Employee Directory / دليل الموظفين (`hr.employee.read`)
  - `hr-contracts`: `/hr/contracts` — Employment Contracts / عقود العمل (`hr.contract.read`, `hr.contract.manage`)
  - `hr-leaves`: `/hr/leaves` — Leave Requests & Balances / الإجازات والمغادرات (`hr.leaves.read`, `hr.leaves.manage`)
  - `hr-attendance`: `/hr/attendance` — Attendance & Timesheets / الحضور والانصراف (`hr.attendance.read`)

#### 7. Payroll (`payroll`) — Category: `hr`
- **Current State**: Payroll runs, payslips, deductions.
- **Target Destinations**:
  - `payroll-runs`: `/payroll` — Monthly Payroll Runs / مسيرات الرواتب (`payroll.run.read`, `payroll.run.manage`)
  - `payroll-payslips`: `/payroll/payslips` — Employee Payslips / قسائم الرواتب (`payroll.payslip.read`)
  - `payroll-adjustments`: `/payroll/adjustments` — Allowances & Deductions / البدلات والاستقطاعات (`payroll.adjustment.manage`)

---

### Wave 4: Specialized Financial Modules

#### 8. Fixed Assets (`fixed-assets`) — Category: `finance`
- **Target Destinations**:
  - `fixed-assets-register`: `/fixed-assets` — Asset Register / سجل الأصول الثابتة
  - `fixed-assets-depreciation`: `/fixed-assets/depreciation` — Depreciation Schedules / مسيرات الإهلاك
  - `fixed-assets-disposals`: `/fixed-assets/disposals` — Disposals & Revaluations / استبعاد وإعادة تقييم الأصول

#### 9. Tax & Compliance (`tax`) — Category: `finance`
- **Target Destinations**:
  - `tax-returns`: `/tax` — Tax Returns & Declarations / الإقرارات الضريبية
  - `tax-withholding`: `/tax/withholding` — Withholding Tax / ضريبة الاستقطاع
  - `tax-settings`: `/tax/settings` — Tax Rates & Categories / فئات وقواعد الضرائب

---

## 3. Rollout Acceptance Checklist

For each migrated module:
- [ ] Registered in `destination-registry.ts` with explicit `NavDestination` array and keywords.
- [ ] Updated module `routes.tsx` with dedicated path-based sub-routes wrapped in `<PermissionGuard>`.
- [ ] Module page component accepts `initialTab` and updates URL route on tab click.
- [ ] Playwright role-based test proves narrow operator (e.g. storekeeper, cashier, junior accountant) only sees permitted tasks while unauthorized tabs/destinations are hidden.
- [ ] Direct navigation to forbidden route fails closed with `<DefaultAccessDeniedView>`.
