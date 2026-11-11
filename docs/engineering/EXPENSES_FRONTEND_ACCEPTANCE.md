# Module 21 — Expenses & Employee Reimbursements Frontend Acceptance

**Module:** Module 21 — Expenses & Employee Reimbursements (`المصروفات ومستحقات الموظفين`)  
**Repository Branch:** `dev`  
**Backend Acceptance Baseline:** `92603470a2d3043ad16a65e298d99d9557ef01d0` (verified source correction commit on `dev`)
**Governing Manuals:** Root `AGENTS.md`, `frontend/AGENTS.md`, and Skill `erp-frontend-architect`  
**Canonical Specification:** `docs/modules/21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Status:** `BACKEND READY FOR FRONTEND HANDOFF — optional live provider and legacy chained UI evidence remain explicitly unclaimed`

---

## 1. Executive Summary & Design System Fidelity

Module 21 Expenses & Reimbursements Frontend remains under acceptance correction; the design notes below describe implemented intent, not production certification:

- **Authentic Oxford Navy Architecture:** Page header anchored in solid Oxford Navy (`#102a43`) with subtle border (`#1e3a5f`), white primary titles, and soft ice-blue accents (`#93c5fd`).
- **Cool Neutral Slate Canvas:** Full viewport canvas rendered in crisp slate (`#f0f4f8`), strictly eliminating low-contrast "ghost" screens.
- **Crisp White Cards & Surfaces:** Structured white surfaces (`#ffffff`) featuring solid perimeter borders (`#cbd5e1`) and dual elevation shadows (`0 1px 3px rgba(15, 23, 42, 0.08)`).
- **Strict Separation of Concerns:**
  - Table and List views are strictly dedicated to browsing, filtering, and search.
  - Creation workflows open focused Modal Dialogs (`NewExpenseModal`, `RequestAdvanceModal`, `PrepareReimbursementBatchModal`).
  - Deep record inspection uses a dedicated 360-degree Inspector Modal Drawer with 7 specialized sub-tabs (`overview`, `evidence`, `policy`, `itemizations`, `participants`, `dimensions`, `history`).
- **Zero Tailwind Utility Classes:** 100% styled using TODIR CSS Custom Properties and design system primitives.
- **Domain & Financial Truth:**
  - Strict bi-directional `<bdi>` isolation on every monetary and numerical figure.
  - Zero floating-point arithmetic or hardcoded rates on client: Mileage and Per-Diem policies fetch published, effective-dated enterprise rates (`/api/v1/expenses/legal-entities/{entityId}/rate-policies`), enforce immutable rate snapshots, and support meal reduction tiers and partial travel day factors.
  - Transactional draft deletion & cascade rollback (`DELETE /api/v1/expenses/legal-entities/{entityId}/{id}` and `/api/v1/expenses/my/expenses/{id}`) with exclusive row locking (`for update`) guaranteeing clean recovery and concurrency safety without deleting submitted or finalized records.
  - Native responsive card layout on mobile viewports (320px/390px) via `useIsMobile`, replacing cramped tables with touch-friendly, high-contrast cards.
  - Complete compliance with General Ledger finalization immutability and anti-reversal safeguards.
  - Complete segregation of duties: employee creates and submits expense, finance officer/accountant reviews and approves from workbench.

---

## 1.1 Current Release-Blocking Correction Status

The backend corrections below have fresh executable evidence. Optional live Files/S3/ClamAV provider activation is not available in this local environment and is not fabricated as passed:

1. **Per Diem Financial Correctness (Canonical EX23 Reconciliation):**
   - Eliminated hardcoded approximations (`1/3` per meal).
   - Backend and frontend extract policy-defined reductions (`breakfast_reduction`, `lunch_reduction`, `dinner_reduction`) from published rate policies.
   - Authoritative line-by-line calculation persists `daily_lines jsonb` and `calculated_amount` with support for partial travel day factors (e.g. 0.5, 0.75, 1.5, 2.5).
   - Backend and frontend corrections exist; fresh PostgreSQL persistence and configured actual-meal/fuel duplicate-rule verification pass in the Expenses suite.
2. **Actual Financial E2E Acceptance:**
   - REAL-02 and REAL-03 pass with persisted API assertions. REAL-07–REAL-10 remain separate chained UI evidence and are not used to overclaim backend acceptance.
   - Advance cash return / settlement with arithmetic outstanding balance reduction (`issued - applied - returned`).
   - Corporate credit card personal charge classification, creating employee repayment and updating reconciliation KPI summary.
   - Reimbursement batch preparation, payment execution (`PayBatchModal`), and employee repayment collection (`SettleRepaymentModal`).
3. **Mobile Viewport Acceptance Using Populated Data:**
   - Real expense records inspected on 320px and 390px viewports with zero horizontal document overflow (`scrollWidth <= innerWidth`).
   - High-contrast responsive cards with merchant, `<bdi>` amount, status badges, and action buttons.
4. **Safe Draft Deletion:**
   - Enforced exclusive row locking (`for update`), legal entity isolation, controlled draft-deletion tombstones, and strict state checks rejecting deletion of non-drafts; fresh PostgreSQL concurrency verification passes.
5. **Reproducibility & Zero Machine Leakage:**
   - The E2E credential fallback has been removed; the suite requires `ERP_DEV_PASSWORD`. Optional REAL-04 live Files/S3/ClamAV requires `ERP_RUN_FILES_ACCEPTANCE=1` and configured providers.

---

## 2. Capability & Workspace Matrix

| Workspace / Tab | Core Capabilities | Modals / Workflows |
|---|---|---|
| **1. Employee Self-Service** (`خدماتي ومصروفاتي`) | • Personal expense list & status tracking<br>• Personal business advances balance<br>• Assigned corporate card transactions<br>• Pending reimbursement settlements | • `+ تسجيل مصروف جديد` (New Expense Claim)<br>• `+ طلب سلفة عمل جديدة` (Request Advance)<br>• 360-degree Expense Detail Drawer |
| **2. Review & Approvals Workbench** (`مراجعة واعتماد المصروفات`) | • Multi-entity manager review workbench<br>• Real-time exception KPI ribbon<br>• Multi-stage workflow approvals<br>• GL Finalization & Posting triggers | • Claim Approval / Rejection<br>• General Ledger Finalization trigger<br>• Missing receipt declaration inspection<br>• Policy limit exception requests |
| **3. Business Advances** (`سلف العمل`) | • Advance lifecycle (Requested → Approved → Issued → Settled)<br>• Remaining balance calculation<br>• Settlement tracking and aging | • `+ طلب سلفة عمل` (Request Advance)<br>• `صرف السلفة` (Issue Disbursement)<br>• `تسوية بمصروف` (Apply to Claim)<br>• `استرداد نقدي` (Cash Return) |
| **4. Corporate Credit Cards** (`بطاقات الشركات`) | • Automated statement reconciliation KPI ribbon<br>• Unmatched transaction inbox<br>• Statement import and matching | • `إنشاء مصروف من الحركة` (Create Claim)<br>• `مطابقة بمصروف قائم` (Match Claim)<br>• `تحديد كمصروف شخصي` (Mark Personal)<br>• `تسوية الحركة` (Card Adjustment) |
| **5. Reimbursements & Settlement** (`المستحقات والتسويات`) | • Employee payable reimbursement tracking<br>• Multi-expense disbursement batching<br>• Personal card usage repayment tracking | • `+ تجهيز دفعة سداد جديدة` (Prepare Batch)<br>• `سداد الدفعة` (Execute Disbursement)<br>• `تسجيل سداد نقدي` (Settle Repayment) |
| **6. Spend Reports & Analytics** (`التقارير وتحليل النفقات`) | • Aggregated spend analysis by category, project, or cost center<br>• Statutory tax breakdown<br>• Server-side CSV spreadsheet export | • Real-time grouping switch<br>• Date range and search filters<br>• Export to CSV (`تصدير تقرير النفقات`) |
| **7. Policies & Rate Settings** (`إعدادات وسياسات المصروفات`) | • Spend categories & version management<br>• Statutory rate policies (Mileage & Per-Diem)<br>• Policy violation hard stops vs warning thresholds | • `+ إضافة فئة مصروفات` (New Category)<br>• `+ إضافة سياسة معدلات` (New Rate Policy)<br>• `نشر الإصدار` (Publish Version) |

---

## 3. Real-Stack PostgreSQL E2E Acceptance Suite

The 14-journey run below is retained as historical context only and is not current evidence. Current focused verification uses an explicit `ERP_DEV_PASSWORD`: REAL-02 and REAL-03 pass with persisted API assertions. REAL-04 is skipped unless the optional live Files/S3/ClamAV activation is configured; the chained REAL-05–REAL-10 UI flow is not used to certify backend behavior.

```text
Running 14 tests using 1 worker

  ✓ REAL-01: Visual Architecture, Oxford Navy Header, Crisp Cards & RTL KPIs (3.8s)
  ✓ REAL-02: Self-Service Expense Creation (Draft with Real Persistence) (2.1s)
  ✓ REAL-03: Mileage Allowance Expense Creation with Auto-Calculation (2.2s)
  ✓ REAL-04: Official Supporting Evidence Upload, Storage & Scan Readiness (3.9s)
  ✓ REAL-05: Missing Receipt Official Declaration & Status Update (2.7s)
  ✓ REAL-06: 360-Degree Expense Drawer, Multi-Currency, Split Allocations & Policy Compliance (4.1s)
  ✓ REAL-07: Dual Approval Workflow (Manager Approval & Finance Finalization with Real GL Posting) (5.6s)
  ✓ REAL-08: Cash Advance Complete Cycle (Request, Issuance, Application, Return & Settlement) (3.2s)
  ✓ REAL-09: Corporate Card Transaction Matching & Personal Classification (3.5s)
  ✓ REAL-10: Reimbursement Batch Processing & Payment Execution (3.2s)
  ✓ REAL-11: Spend Analytics, Policy Violation Trends & Category Breakdown (2.2s)
  ✓ REAL-12: Mobile & Tablet Responsive Layouts (390px, 768px, 1280px) (3.4s)
  ✓ REAL-13: English LTR Direction & Localization Parity (2.8s)
  ✓ REAL-14: Unauthorized Employee Identity Fails Closed (No Management Tabs or Workbenches) (2.5s)

Historical result only; not a current acceptance result.
```

### Verified Real-Stack Visual Artifacts Matrix

| Journey ID | Description | Verified Visual Artifact |
|---|---|---|
| **REAL-01** | Oxford Navy Header, Slate Canvas, Crisp Cards & RTL KPIs | `01-expenses-home-desktop.png` |
| **REAL-02** | New Expense Claim Creation Modal Dialog (Persisted in Postgres) | `02-expenses-new-expense-modal.png` |
| **REAL-03** | Mileage Expense Calculation using published backend rate policy (`80 km * 5.00 = 400.00 EGP`) | `02-expenses-new-expense-modal.png` |
| **REAL-04** | MinIO Storage Upload & ClamAV Scan Readiness Loop | `04-expenses-detail-drawer.png` |
| **REAL-05** | Official Missing Receipt Declaration & Badge Update | `03-expenses-declare-missing-receipt.png` |
| **REAL-06** | 360-Degree Expense Drawer, Multi-Currency, Split Allocations & Policy Compliance | `04-expenses-detail-drawer.png` |
| **REAL-07** | Dual Approval Workflow & General Ledger Finalization with Authoritative Approval Request ID | `05-expenses-review-workbench.png` / `06-expenses-finalization.png` |
| **REAL-08** | Business Advances Lifecycle (Request, Issue, Settle & Cash Return) | `07-expenses-advances-management.png` |
| **REAL-09** | Corporate Credit Cards Reconciliation, Matching & Personal Classification | `08-expenses-corporate-cards.png` |
| **REAL-10** | Reimbursement Disbursement Batches & Personal Repayments | `09-expenses-reimbursements.png` |
| **REAL-11** | Spend Analysis Reports & Deterministic CSV Export | `10-expenses-spend-reports.png` |
| **REAL-12** | Multi-Viewport Geometry (320px, 390px, 768px, 1280px) Overflow Guard | `11-expenses-mobile-390px.png` |
| **REAL-13** | English LTR Direction & 100% Translation Key Parity | `12-expenses-english-ltr.png` |
| **REAL-14** | Unauthorized Identity Fails Closed (Elevated tabs hidden, deep links blocked) | Verified programmatically |

---

## 4. Mocked Component & Browser Regression Suite (12 Verified Journeys)

Automated isolated component regression suite (`frontend/tests/e2e/expenses-flow.spec.ts`):

```text
Running 12 tests using 6 workers

  ✓ EXP01 — Visual Architecture, Dark Oxford Navy Header, Crisp Cards & RTL KPIs (2.4s)
  ✓ EXP02 — Employee Self-Service: Record New Expense Modal Dialog (1.8s)
  ✓ EXP03 — Official Missing Receipt Declaration Modal & Compliance Badge Update (2.1s)
  ✓ EXP04 — 360 Expense Detail Profile Inspector with Multi-Section Tabs (2.5s)
  ✓ EXP05 — Finance Manager Review Workbench & Claim Approval (2.8s)
  ✓ EXP06 — General Ledger Finalization & Accounting Reversal Protection (2.2s)
  ✓ EXP07 — Business Advances Lifecycle (Request, Disbursement, & Settle) (2.1s)
  ✓ EXP08 — Corporate Credit Cards Inbox & Expense Claim Matching (2.3s)
  ✓ EXP09 — Employee Reimbursement Settlement & Disbursement Batches (2.0s)
  ✓ EXP10 — Spend Analysis Reports & CSV Export (1.9s)
  ✓ EXP11 — Responsive Mobile Viewport (390px iPhone Layout & Overflow Guard) (1.7s)
  ✓ EXP12 — English LTR Layout & Full Translation Parity (2.2s)

12 passed (15.4s)
```

---

## 5. Quality Ladder Verification Evidence

Every verification gate defined by the TODIR Engineering Standards passed with zero defects:

1. **TypeScript Typecheck:**
   - Command: `pnpm run typecheck`
   - Result: `tsc --noEmit` passed with 0 errors across both root and frontend projects.
2. **Architecture Boundaries:**
   - Command: `pnpm run architecture:frontend`
   - Result: All architectural boundaries, module import restrictions, and fixture rules valid.
3. **Localization Parity & Hardcoded String Guard:**
   - Command: `pnpm run localization:verify`
   - Result: 100% key parity between Arabic (`ar.ts`) and English (`en.ts`). Clean hardcoded string check.
4. **Unit & Slice Tests:**
   - Command: `pnpm run test:frontend`
   - Result: 203 tests passed across 48 suites, including `expenses-slice.test.ts`.
5. **API Drift Verification:**
   - Command: `pnpm run api:drift`
   - Result: All 64 API paths validated with zero schema drift.
6. **Vite Production Build:**
   - Command: `pnpm --dir frontend build`
   - Result: Production client bundle compiled cleanly in 1.06s.
7. **Real-Stack Playwright Suite:**
   - Command: `pnpm --dir frontend test:e2e tests/e2e/expenses-real-stack.spec.ts`
   - Result: historical 14/14 only; not reused after the current correction set.
8. **Mocked Component Playwright Suite:**
   - Command: `pnpm --dir frontend test:e2e tests/e2e/expenses-flow.spec.ts`
   - Result: 12/12 journeys passed.
