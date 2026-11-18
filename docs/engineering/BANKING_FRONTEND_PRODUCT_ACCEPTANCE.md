# Module 25 — Banking & Cash Management Product & UX Acceptance Report

**Module:** Module 25 — Banking & Cash Management (`العمليات المصرفية وإدارة النقدية`)  
**Repository Branch:** `dev`  
**Git Remote:** `origin/dev`  
**Canonical Specification:** `docs/modules/25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Gate Status:** `MODULE 25 BANKING FRONTEND + UX COMPLETE — READY FOR PRODUCT OWNER ACCEPTANCE`

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

## 2. Real-Stack User Journeys & Acceptance Evidence

### Journey 1: Cash Position Overview, Real Aggregates & Cash-to-Ledger Control
- Authenticated user (`admin@atlas.com`) views live aggregated metrics from PostgreSQL.
- **Verification:** Hardcoded metrics and fake percentages were completely purged. Real metrics bind to `GET /api/v1/banking/cash-position` with legal-entity scoping. Cash-to-Ledger control card displays GL reconciliation state.
- **Artifact:** `artifacts/banking-acceptance/real-stack/01-banking-overview-desktop.png`

### Journey 2: Treasury Account Lifecycle, Filterable Register & 360° Inspector Drawer
- Treasury managers review Bank, Cash Vault, and Payment Gateway accounts.
- **Verification:** Filter by account type (`بنك`, `خزينة نقدية`, `بوابة دفع`). Clicking a row opens the 360° Inspector Drawer with 4 operational tabs: Overview, Balance & General Ledger, Signatories & Limits, and Activity Audit.
- **Artifact:** `artifacts/banking-acceptance/real-stack/02-treasury-account-detail-drawer.png`

### Journey 3: Bank Statement Import & Statement Line Classification
- Finance users import statements via RFC 4180 compliant CSV parser supporting quoted strings, escaped quotes `""`, embedded commas, and CRLF linebreaks with row-specific validation error reporting, or genuine manual line entry. Synthetic generators and hardcoded counterparties were completely removed.
- **Verification:** Statement lines render in widened dialog (`min(96vw, 1100px)`) with classification badges (`مطابق لقاعدة`, `مطابق تلقائياً`, `يحتاج مراجعة`, `يدوي`), explicit column `minWidth`s, horizontal scrolling, and `<BdiText>` formatting.
- **Artifact:** `artifacts/banking-acceptance/real-stack/03-statement-lines-classification.png`

### Journey 4: Reconciliation Workbench & Conservation Arithmetic
- Accountants match bank statement lines against system general ledger records fetched dynamically from `/api/v1/payments`, matching currency and debit/credit polarity. Resolves the exact linked statement with zero arbitrary fallback to first statement.
- Cross-currency selection is strictly forbidden. Live session refetch dynamically updates persisted difference and version, with 409 conflict detection and reload action. Authoritative finalization rule enforced (`difference === 0 && matchCount > 0`).
- **Verification:** Two-sided matching grid in widened modal (`min(96vw, 1250px)`) displays bank transactions vs ledger records. Strict conservation arithmetic bar displays: `Bank Balance - Outstanding Withdrawals + Outstanding Deposits = GL Balance`.
- **Artifact:** `artifacts/banking-acceptance/real-stack/04-reconciliation-workbench-matching.png`

### Journey 5: Cash & Vault Management, Denomination Grid & Variance Settlement
- Cashiers and treasury supervisors manage physical vaults and conduct count sessions. Expected balance pre-fills dynamically from the active vault book balance (`useCashPosition`), and custodian is verified via authenticated session context badge (zero editable raw UUIDs).
- Duplicate cutoff dates are gracefully intercepted and mapped to `banking.cash_count_duplicate`.
- **Verification:** Interactive denomination breakdown grid calculates total currency breakdown (200, 100, 50, 20, 10, 5). Over/short variance triggers the dedicated `تسوية الفروقات النقدية` modal with required reason and destination account.
- **Artifact:** `artifacts/banking-acceptance/real-stack/05-cash-count-denomination-grid.png`

### Journey 6: Internal Transfers, Transit Tracking & Gateway Settlements
- Operations team monitors funds transferred between bank accounts and cash vaults.
- Account selection uses searchable active accounts picker (`status === "Active"`), with destination strictly excluding the source account, real-time validation ensuring `amount > 0`, and stable idempotency keys.
- **Verification:** Transfers table displays in-transit status and transit accounts. Clicking "تسوية التحويل" opens the settlement dialog requiring settlement date, reference number, and evidence file reference.
- **Artifact:** `artifacts/banking-acceptance/real-stack/06-internal-transfer-settlement.png`

### Journey 7: Liquidity Forecasting & Financial Code Mappings
- Financial controllers project future liquidity across multiple horizons, with KPI totals strictly partitioned per currency code.
- **Verification:** Rolling cash forecast selects 30, 60, and 90-day horizons with projected inflows, outflows, and net liquidity curves. Banking transaction code mappings grid displays GL mapping rules.
- **Artifact:** `artifacts/banking-acceptance/real-stack/07-cash-forecast-horizon.png`

### Journey 8: Responsive Mobile Ergonomics (390px Viewport)
- Mobile users and field cashiers access banking operations on phone screens.
- **Verification:** Tested at 390x844 viewport. Metrics stack cleanly into single-column cards, navigation tabs wrap without horizontal clipping, tables scroll within defined card boundaries with zero page-level horizontal overflow.
- **Artifact:** `artifacts/banking-acceptance/real-stack/08-banking-mobile-390px.png`

### Journey 9: English LTR Parity & Corporate Visual Integrity
- International finance users toggle application language to English (`en`).
- **Verification:** Direction shifts to LTR, typography aligns to Inter/Roboto, all labels and status badges translate cleanly with 100% dictionary coverage, Oxford Navy header and slate card design maintain full visual hierarchy.
- **Artifact:** `artifacts/banking-acceptance/real-stack/09-banking-english-ltr.png`

---

## 3. Product Owner Verification Instructions

To inspect the real live Banking & Cash Management workspace locally:

1. **Start Services:**
   - Ensure backend API is running: `dotnet run --project backend/Api --urls "http://127.0.0.1:5080"`
   - Ensure frontend Vite dev server is running: `pnpm --dir frontend run dev --port 5173`
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
