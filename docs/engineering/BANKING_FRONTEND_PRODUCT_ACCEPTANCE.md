# Module 25 — Banking & Cash Management Product & UX Acceptance Report

**Module:** Module 25 — Banking & Cash Management (`العمليات المصرفية وإدارة النقدية`)  
**Repository Branch:** `dev`  
**Git Remote:** `origin/dev`  
**Canonical Specification:** `docs/modules/25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Gate Status:** `MODULE 25 BANKING FRONTEND + UX COMPLETE — READY FOR PRODUCT OWNER ACCEPTANCE`  
**Verification Environment:** Live .NET API (`http://127.0.0.1:5080`) backed by PostgreSQL acceptance database (`todir_module25_acceptance` on port 15433). Zero writes to populated development database `todir`.

---

## 1. Product & Architecture Design Standards

### 1.1 Visual Hierarchy & Corporate Palette
- **Application Shell:** Anchored by the corporate Dark Oxford Navy header (`#102a43`), with ice-blue active accents (`#38bdf8`) and pure white headings (`#ffffff`).
- **Canvas & Contrast:** Crisp slate background canvas (`#f0f4f8`), eliminating low-contrast or borderless "ghost" cards.
- **Card Styling:** Solid crisp white cards (`#ffffff`) with 1px perimeter border (`#cbd5e1`), dual-layer elevation shadows, and accent borders denoting state.
- **Strict Separation of Concerns:**
  - Table and List views are strictly dedicated to browsing, filtering, and search.
  - Creation workflows open focused Modal Dialogs (`NewAccountModal`, `ImportStatementModal`, `CustodyTransferModal`, `NewTransferModal`).
  - Deep inspection uses a dedicated 360-degree Inspector Modal Drawer (`TreasuryAccountDetailDrawer`).
  - Zero mixed read/write collision across all workspaces.

### 1.2 Enterprise Arabic Vocabulary Compliance
All user-facing Arabic microcopy strictly follows the Enterprise Arabic Vocabulary Charter:
- `إدارة البنوك والنقدية` for Banking & Cash Management (never literal transliteration).
- `حساب خزانة / بنك` for Treasury Account.
- `كشف حساب بنكي` for Bank Statement.
- `مطابقة كشف الحساب البنكي` for Bank Reconciliation (never `تسوية البنك`).
- `خزينة نقدية` for Cash Vault.
- `تسوية الفروقات النقدية` for Cash Variance Settlement.
- `فئات النقدية` for Currency Denominations.
- `تحويل بين الحسابات` for Internal Transfer.
- `توقع السيولة النقدية` for Cash Flow Forecast.
- `ميزان المراجعة` for Trial Balance.
- `قيد يومية` for Journal Entry.

---

## 2. Four-Point Financial Integrity Deliverables

### Point 1: Truthful Cash Count Identity & Balance
- **Eliminated Fake Custodian Fallback:** Removed hardcoded UUID `00000000-0000-0000-0000-000000000001`. Custodian identity is dynamically resolved from active user context (`effectiveUserId = outlet?.userId || user?.userId`).
- **Backend-Derived Expected Balance:** Expected balance is derived directly from the active cash position (`useCashPosition`) linked to the vault's ledger snapshot. Removed any fallback coercing missing balance to zero.
- **Read-Only Financial Baseline:** Users cannot manually fabricate an expected balance. When custodian or expected balance is unavailable, the UI renders an unavailable alert state and blocks submission.

### Point 2: Payment Matching Across Accounts & Server Pagination
- **Multi-Factor Payment Discovery:** Extended payment querying to respect treasury account (`paymentAccountId`), legal entity (`legalEntityId`), currency (`currency`), direction (`direction`), and eligible "Completed" state.
- **Preserved Selections Across Pagination & Search:** Implemented `selectedPaymentsMap` in `reconciliation-workbench-modal.tsx` to retain selected payments, matched amounts, and partial allocation amounts across server-side pagination and search changes.
- **Remaining Matchable Calculation via Claims Query:** Added `GET /api/v1/banking/reconciliations/{id}/claims` to accurately retrieve claims per payment, ensuring payments cannot be over-claimed across sessions.
- **Cross-Currency Prevention:** Prevents selecting or matching transactions with mismatched currencies or directions.
- **Authoritative Backend Finalization:** Live session refetch dynamically updates difference and version; finalization enforces `difference === 0 && outstanding === 0` authoritatively on the backend.

### Point 3: Proved Missing Business Transitions (Tests 10–15)
- Added 6 focused persisted-state acceptance tests (10 through 15) to `frontend/tests/e2e/banking-real-stack.spec.ts`. All 15 tests pass against the isolated acceptance database (`todir_module25_acceptance`).

### Point 4: Accurate Documentation & Honest Acceptance
- Reconciled all frontend coverage matrices, routes, and DTOs with actual backend endpoints (`/statements/import`, `/claims`, `/treasury-accounts`).
- Verified zero synthetic generators and zero mocked APIs.

---

## 3. Real-Stack User Journeys & Acceptance Evidence (15/15 PASS)

### Journey 1: Cash Position Overview, Real Aggregates & Cash-to-Ledger Control
- Authenticated user views live aggregated metrics from PostgreSQL. Real metrics bind to `GET /api/v1/banking/cash-position` with legal-entity scoping. Cash-to-Ledger control card displays GL reconciliation state.
- **Artifact:** `artifacts/banking-acceptance/real-stack/01-banking-overview-desktop.png` (`PASS`)

### Journey 2: Treasury Account Lifecycle, Filterable Register & 360° Inspector Drawer
- Treasury managers review Bank, Cash Vault, and Payment Gateway accounts. Filter by account type (`بنك`, `خزينة نقدية`, `بوابة دفع`). Clicking a row opens the 360° Inspector Drawer with 4 operational tabs: Overview, Balance & General Ledger, Signatories & Limits, and Activity Audit.
- **Artifact:** `artifacts/banking-acceptance/real-stack/02-treasury-account-detail-drawer.png` (`PASS`)

### Journey 3: Bank Statement Import & Statement Line Classification
- Finance users import statements via RFC 4180 compliant CSV parser supporting quoted strings, escaped quotes `""`, embedded commas, and CRLF linebreaks with row-specific validation error reporting, or genuine manual line entry. Synthetic generators were completely removed.
- **Artifact:** `artifacts/banking-acceptance/real-stack/03-statement-lines-classification.png` (`PASS`)

### Journey 4: Reconciliation Workbench & Conservation Arithmetic
- Accountants match bank statement lines against system general ledger records fetched dynamically from `/api/v1/payments`, matching currency and debit/credit polarity. Resolves the exact linked statement with zero arbitrary fallback to first statement.
- **Artifact:** `artifacts/banking-acceptance/real-stack/04-reconciliation-workbench-matching.png` (`PASS`)

### Journey 5: Cash & Vault Management, Denomination Grid & Variance Settlement
- Cashiers and treasury supervisors manage physical vaults and conduct count sessions. Expected balance pre-fills dynamically from the active vault book balance (`useCashPosition`), and custodian is verified via authenticated session context badge (zero editable raw UUIDs).
- **Artifact:** `artifacts/banking-acceptance/real-stack/05-cash-count-denomination-grid.png` (`PASS`)

### Journey 6: Internal Transfers, Transit Tracking & Gateway Settlements
- Operations team monitors funds transferred between bank accounts and cash vaults. Account selection uses searchable active accounts picker (`status === "Active"`), with destination strictly excluding the source account, real-time validation ensuring `amount > 0`, and stable idempotency keys.
- **Artifact:** `artifacts/banking-acceptance/real-stack/06-internal-transfer-settlement.png` (`PASS`)

### Journey 7: Liquidity Forecasting & Financial Code Mappings
- Financial controllers project future liquidity across multiple horizons, with KPI totals strictly partitioned per currency code. Rolling cash forecast selects 30, 60, and 90-day horizons with projected inflows, outflows, and net liquidity curves.
- **Artifact:** `artifacts/banking-acceptance/real-stack/07-cash-forecast-horizon.png` (`PASS`)

### Journey 8: Responsive Mobile Ergonomics (390px Viewport)
- Mobile users and field cashiers access banking operations on phone screens (390x844 viewport). Metrics stack cleanly into single-column cards, navigation tabs wrap without clipping, tables scroll within defined card boundaries with zero page-level horizontal overflow.
- **Artifact:** `artifacts/banking-acceptance/real-stack/08-banking-mobile-390px.png` (`PASS`)

### Journey 9: English LTR Parity & Corporate Visual Integrity
- International finance users toggle application language to English (`en`). Direction shifts to LTR, typography aligns to Inter/Roboto, all labels and status badges translate cleanly with 100% dictionary coverage.
- **Artifact:** `artifacts/banking-acceptance/real-stack/09-banking-english-ltr.png` (`PASS`)

### Journey 10: Real Bank Statement & Payment Matching (Persisted State Verification)
- Creates dedicated bank account (`BANK-MATCH-E2E`), imports bank statement with incoming credit line of 150 EGP, completes an incoming payment of 150 EGP via Payments module, creates reconciliation session linked to the account, matches statement line to the payment, and asserts persisted session state (`MatchedLines == 1`, `OutstandingLines == 0`, `Difference == 0`). (`PASS`)

### Journey 11: Authorized Reconciliation Finalization & Invariant Guards
- Creates dedicated bank account (`BANK-FIN-E2E`), completes matching incoming payment of 250 EGP, imports matching statement, executes match, finalizes reconciliation session via `POST /api/v1/banking/reconciliations/{id}/finalize`, verifies persisted state is `'Finalized'`, and verifies backend invariant guard rejects further line matching on a finalized session. (`PASS`)

### Journey 12: Cash Count Submission, Approval & Difference Resolution Lifecycle
- Selects active cash vault, obtains expected balance (2,500 EGP), submits physical cash count with denomination breakdown totaling 1,000 EGP (creating -1,500 EGP variance). Verifies DB-persisted count session (`Status == 'Submitted'`). Approves cash count via `POST /api/v1/banking/cash-counts/{id}/approve` (state becomes `'Approved'`). Resolves variance via `POST /api/v1/banking/cash-variances/settle`. (`PASS`)

### Journey 13: Treasury Transfer via Payments & Banking Settlement Lifecycle
- Executes internal transfer between treasury accounts via `POST /api/v1/payments/transfers`, verifies creation and state of transfer projection record in `finance_banking.transfer_reconciliation_projection` (`state == 'Matched'`), and verifies settlement state invariants. (`PASS`)

### Journey 14: Liquidity Forecast Version & Manual Items Persistence
- Creates a new Draft cash forecast version with horizon days via `POST /api/v1/banking/cash-forecast/versions`, adds manual inflow and outflow items with financial codes via `POST .../items`, and verifies persistence in PostgreSQL. (`PASS`)

### Journey 15: Cross-Entity Access Rejection & Security Isolation
- Queries foreign/unowned legal entity treasury account and asserts access rejection (404/403). Submits cross-entity mutation without valid context and asserts rejection. Unauthenticated requests assert 401 Unauthorized. Invalid anti-forgery tokens assert 400 rejection. (`PASS`)

---

## 4. Product Owner Verification Instructions

To inspect the real live Banking & Cash Management workspace locally:

1. **Start Services:**
   - Ensure backend API is running against isolated acceptance database:
     `dotnet run --project backend/Api --urls "http://127.0.0.1:5080"`
   - Ensure frontend Vite dev server is running:
     `pnpm --dir frontend run dev --port 5173`
2. **Access Application:**
   - Open browser to `http://127.0.0.1:5173`
   - Sign in as `admin@atlas.com` with development credentials.
   - Navigate to **العمليات المصرفية** (`/banking`) in the sidebar navigation.
3. **Verify Acceptance Criteria:**
   - Check Overview KPI cards and Cash-to-Ledger status.
   - Click any Treasury Account to inspect the 360° Drawer.
   - Switch between Statements, Reconciliation, Cash & Vaults, Transfers, and Forecasting tabs.
   - Test the Denomination Breakdown Grid on Cash Management tab.
   - Toggle language to English (`EN`) to verify LTR layout and typography.
