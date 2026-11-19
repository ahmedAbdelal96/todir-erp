# Module 25 — Banking & Cash Management Frontend Coverage Matrix

**Module:** Module 25 — Banking & Cash Management (`العمليات المصرفية وإدارة النقدية`)  
**Canonical Specification:** `docs/modules/25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Coverage Scope:** Frontend + UX + Real-Backend Integration & Database-Persisted State Transitions  
**Verification Environment:** Isolated acceptance database `todir_module25_acceptance` on port 15433 (zero mutations to populated `todir`)  

---

## Complete Capability Coverage Matrix

| Requirement / Capability | Business Rule & Domain Invariant | Persistence & Schema | Backend API Contract | Frontend Component / Tab | Automated Test | Evidence Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Cash Position Overview & KPIs** | Live aggregated balance metrics; multi-currency breakdown; legal-entity scoping; Cash-to-Ledger control card; preserves missing balance integrity (never coerces null to 0); coverage & freshness tracking; restricted backend configuration exception handling | `finance_banking.treasury_account`, `cash_position` queries | `GET /api/v1/banking/cash-position`, `GET /api/v1/banking/reports/cash-to-ledger` | `BankingOverviewTab` | Real-Stack E2E `01` | **PASS** |
| **Treasury Accounts Register & Filter** | Registry across Bank, Cash Vault, Payment Gateway, and Clearing accounts; status filtering; account details | `finance_banking.treasury_account` | `GET /api/v1/banking/treasury-accounts`, `POST /api/v1/banking/treasury-accounts` | `TreasuryAccountsTab`, `NewAccountModal` | Real-Stack E2E `02` + Unit Slice | **PASS** |
| **360° Account Inspector Drawer** | Deep tabbed inspection: Overview, Balances & GL linkage, Authorized Signatories & Limits, Activity Audit Trail | `finance_banking.treasury_account`, `account_audit` | `GET /api/v1/banking/treasury-accounts/{id}` | `TreasuryAccountDetailDrawer` | Real-Stack E2E `02` | **PASS** |
| **Bank Statements Import & Parsing** | RFC 4180 compliant CSV parser supporting quoted strings, escaped quotes `""`, embedded commas, and CRLF/LF linebreaks; row-specific validation error reporting; genuine manual entry fallback; widened dialog with explicit table column minWidths | `finance_banking.bank_statement` | `GET /api/v1/banking/statements`, `POST /api/v1/banking/statements/import` | `StatementsTab`, `ImportStatementModal` | Real-Stack E2E `03` + Unit Slice (CSV Parser) | **PASS** |
| **Statement Lines Classification** | Real classification engine (`Rule-Matched`, `Auto-Matched`, `Needs Review`, `Manual`); audit line review; widened dialog (`min(96vw, 1100px)`) with horizontal scrolling and `<BdiText>` | `finance_banking.bank_statement_line` | `GET /api/v1/banking/statements/{id}/lines` | `StatementsTab`, `StatementLinesModal` | Real-Stack E2E `03` | **PASS** |
| **Reconciliation Workbench & Matching** | Resolves exact linked statement (zero arbitrary `items[0]` fallback); multi-page selection map preserving payments and matched amounts across search and pagination; server-side payment discovery filtered strictly by treasury account, legal entity, currency, direction, and eligible "Completed" state; remaining matchable amount calculated via `/claims` query; strict cross-currency selection prevention; live session refetch; 409 conflict detection; authoritative finalization rule (`difference === 0 && outstanding === 0`) | `finance_banking.reconciliation_session`, `reconciliation_statement_claim` | `GET /api/v1/banking/reconciliations`, `GET /api/v1/banking/reconciliations/{id}/claims`, `POST /api/v1/banking/reconciliations/{id}/match`, `POST /api/v1/banking/reconciliations/{id}/finalize` | `ReconciliationTab`, `ReconciliationWorkbenchModal` | Real-Stack E2E `04`, `10`, `11` | **PASS** |
| **Conservation Arithmetic Equation** | Equation indicator: `Bank Balance - Outstanding Withdrawals + Outstanding Deposits = GL Balance`; discrepancy highlighted; authoritative finalization guard | In-memory verified formula | Reconciliation summary DTOs | `ReconciliationTab`, `ReconciliationWorkbenchModal` | Real-Stack E2E `04` + Unit Slice | **PASS** |
| **Cash Vaults & Custody Management** | Physical vaults register; custodian assignments; custody handover tracking | `finance_banking.cash_vault`, `custody_assignment` | `GET /api/v1/banking/vaults` | `CashManagementTab`, `CustodyTransferModal` | Real-Stack E2E `05` | **PASS** |
| **Truthful Cash Count & Denomination Breakdown** | Real authenticated custodian context (zero hardcoded fallback UUIDs); backend-derived expected balance via `useCashPosition` (read-only, never coerces missing balance to zero); submission blocked if custodian or balance missing; interactive denomination breakdown grid (200, 100, 50, 20, 10, 5); duplicate cutoff date conflict handling mapped to `banking.cash_count_duplicate` | `finance_banking.cash_count`, `cash_count_denomination` | `POST /api/v1/banking/cash-counts` | `CashManagementTab` Denomination Grid, `CashCountModal` | Real-Stack E2E `05`, `12` + Unit Slice | **PASS** |
| **Cash Variance Settlement & Approval** | Over/Short variance calculation against expected vault balance; settlement journal creation workflow; supervisor approval and difference resolution lifecycle | `finance_banking.cash_variance`, `cash_count` | `POST /api/v1/banking/cash-counts/{id}/approve`, `POST /api/v1/banking/cash-variances/settle` | `CashManagementTab`, `SettleVarianceModal` | Real-Stack E2E `05`, `12` | **PASS** |
| **Internal Treasury Transfers** | Searchable active accounts picker (`status === "Active"`); destination excludes source account; validation `amount > 0` and same-account rejection; stable idempotency keys; in-transit tracking; fee deduction tracking | `finance_banking.treasury_transfer`, `transfer_reconciliation_projection` | `GET/POST /api/v1/banking/transfers`, `POST /api/v1/payments/transfers` | `TransfersTab`, `NewTransferModal` | Real-Stack E2E `06`, `13` | **PASS** |
| **Transfer Settlement & Evidence** | In-transit tracking; settlement requires documentary evidence and transaction reference; rejects invalid settlement transitions | `finance_banking.treasury_transfer` | `POST /api/v1/banking/transfers/{id}/settle` | `TransfersTab`, `SettleTransferModal` | Real-Stack E2E `06`, `13` | **PASS** |
| **Liquidity & Cash Forecasting** | Rolling horizon (30 / 60 / 90 days); projected inflows and outflows; net liquidity curve; version creation ('Draft'/'Published'); manual forecast items persistence | `finance_banking.liquidity_forecast`, `cash_forecast_version`, `cash_forecast_manual_item` | `GET /api/v1/banking/forecasting/liquidity`, `POST /api/v1/banking/cash-forecast/versions`, `POST /api/v1/banking/cash-forecast/versions/{id}/items` | `ForecastingTab` | Real-Stack E2E `07`, `14` | **PASS** |
| **Financial Code Mappings** | Treasury transaction code to general ledger account mapping rules | `finance_banking.banking_code_mapping` | `GET /api/v1/banking/code-mappings` | `ForecastingTab` (Code Mappings Grid) | Real-Stack E2E `07` | **PASS** |
| **Cross-Entity Security Isolation** | Requests scoped to foreign or non-existent legal entities reject access (404/403); unauthenticated requests reject (401); invalid anti-forgery tokens reject (400) | PostgreSQL tenant & entity isolation | Authoritative backend middleware & `IPermissionAuthorizer` | All Banking Endpoints | Real-Stack E2E `15` | **PASS** |
| **Responsive Mobile Layout (390px)** | Touch-friendly cards, high contrast, responsive grid collapse, zero horizontal overflow | Design System tokens | Viewport media queries & CSS modules | All Banking Tabs | Real-Stack E2E `08` | **PASS** |
| **Authentic Arabic-First & English LTR** | 100% dictionary key parity, authentic market vocabulary, zero literal machine translations | `ar.ts` & `en.ts` dictionaries | Localization verification script | Full Banking Module | Real-Stack E2E `09` + `verify-localization.mjs` | **PASS** |

---

## Test Suites Summary

| Test Suite | Purpose / Scope | Target Environment | Scenarios Passed | Execution Time |
| :--- | :--- | :--- | :--- | :--- |
| **Real-Stack E2E (`banking-real-stack.spec.ts`)** | Authentic live database-backed end-to-end acceptance covering all 15 operational and persisted-state journeys with zero mocked business APIs and target safety probe guard | Live .NET API (`:5080`) + PostgreSQL (`finance_banking`) on isolated acceptance DB (`:15433`) | **15 / 15 PASS** | 54.7s |
| **Unit & Slice Tests (`banking-slice.test.ts`)** | RFC 4180 CSV parser, denomination arithmetic, conservation equation, cash position stale detection, internal transfer transit states, custodian & balance integrity guards, multi-page payment selection preservation | Node.js Test Runner | **9 / 9 PASS** | 155ms |
| **Full Frontend Unit Suite (`test:frontend`)** | Repository-wide frontend unit and regression coverage | Node.js Test Runner | **238 / 238 PASS** | 2.8s |
| **Architecture Boundaries (`architecture:frontend`)** | Architectural invariants, import boundaries, package isolation | Static analysis | **PASS** | 1.0s |
| **Localization Verification (`verify-localization.mjs`)** | Arabic/English key parity, hardcoded string guards | Static analysis script | **PASS** | 0.8s |
| **TypeScript Strict Checking (`typecheck`)** | Strict compile-time type verification | TypeScript Compiler (`tsc`) | **PASS (0 errors)** | 3.5s |
| **Production Bundle Compilation (`build`)** | Vite production assets build and chunk validation | Vite bundler | **PASS** | 1.3s |

---

## Definition of Done (DoD) Verification

- [x] **DB Coverage:** Full schema mapping in `finance_banking` schema (37 PostgreSQL tables).
- [x] **Backend Coverage:** All C# DTO contracts implemented and verified (`dotnet test` passed).
- [x] **Development Seed Coverage:** Seeded development scenarios available for local testing (`Atlas` / `Delta`).
- [x] **Frontend Coverage:** 6 functional workspace tabs, 5 modal workflows, 360-degree drawer inspector.
- [x] **Real-Stack Acceptance:** 15 end-to-end database-backed user journeys pass against live .NET backend.
- [x] **Product/UX Acceptance:** Codified Oxford Navy / slate design system, zero Tailwind, RTL native, responsive 390px mobile viewports.
