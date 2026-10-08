# 28 — TODIR ERP SaaS Budgeting, Financial Planning & FP&A Requirements & Technical Design Pack

**File:** `28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + BUDGETING + FORECASTING + SCENARIO PLANNING + DRIVER MODELS + BUDGET CONTROL + WORKFORCE PLAN + CAPEX PLAN + INTEGRATED FINANCIAL PLAN + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-07  
**Risk Class:** **CRITICAL — BUDGET AUTHORITY + FORECASTS + COMMITMENTS + CASH + PROFITABILITY + MANAGEMENT DECISIONS**

## Canonical TODIR dependencies

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:

```text
Actual financial truth                → Ledger / Accounting
Project Budget / ETC / EAC            → Projects
Short-term treasury forecast          → Banking
Production / MRP plan                 → Manufacturing
Employee / Employment                 → HR
Actual Payroll / Payslip              → Payroll
Asset / Depreciation books            → Fixed Assets
Purchase commitment                   → Purchasing
Sales transaction                     → Sales
Company-wide Budget / Forecast        → 28 FP&A
```

Planning never posts accounting directly. Budget Control never replaces owning-module approval. Approved plan history is never silently overwritten.

---
# 0. Executive Decision

TODIR `28 Budgeting, Financial Planning & FP&A` is the canonical company-wide planning capability that answers:

> **What are we trying to achieve financially, what assumptions drive that plan, what has actually happened, what is now expected to happen, where are we above/below plan, which commitments consume budget, what happens under alternative scenarios, and what decisions are required now?**

It supports annual budgets, rolling forecasts, long-range plans, scenarios, Revenue/OPEX/Workforce/CapEx planning, budget control, variance analysis and integrated projected statements.

---
# 1. Product Principle

> **Plan from explicit assumptions, compare continuously to Actual, reforecast without destroying the approved baseline, and make every management number explainable.**

---
# 2. Small Company Flow

```text
Create FY2027 Budget
→ Revenue
→ Main Expense Categories
→ Payroll
→ CapEx
→ Review P&L
→ Approve
→ Compare Actual vs Budget
```
No EPM administration burden is required.

---
# 3. Large Company Flow

A larger organization can progressively enable:

```text
Department submissions
Top-down targets
Bottom-up plans
Driver models
Scenario planning
Multi-entity rollups
Budget control
Commitment consumption
Workforce planning
CapEx planning
Rolling forecasts
Long-range planning
Integrated statements
Allocations
SoD
```

---
# 4. Locked Distinctions

```text
Actual != Budget
Budget != Forecast
Forecast != Scenario
Target != Approved Budget
Scenario != Published Forecast
Original Budget != Current Budget
Budget Revision != Budget Transfer
Corporate Budget != Project Budget
Financial Cash Forecast != Treasury Cash Forecast
Revenue Plan != Sales Order
Headcount Plan != Employee
Planned Payroll Cost != Payslip
CapEx Plan != Fixed Asset
Planned Purchase != Purchase Order
Budget Check != Purchase Approval
Planning FX != Actual Accounting FX
Planning Rollup != Statutory Consolidation
Assumption != Plan Value
Commitment != Actual
AI Suggestion != Approved Forecast
```

---
# 5. Actual

Actual is what happened in canonical operational/accounting domains. Actual values shown in `28` are read-only projections or snapshots from source truth.

---
# 6. Budget

Budget is an approved planned/control baseline. Actual spending never overwrites it.

---
# 7. Forecast

Forecast answers: **Given Actual to date and updated expectations for the future, where do we now expect to finish?**

---
# 8. Target

A Target is management aspiration or constraint, such as `Revenue 150M` or `EBITDA Margin 18%`; it need not equal the approved Budget.

---
# 9. Scenario

A Scenario is an alternative set of assumptions/outcomes such as Base, Upside, Downside or FX Stress. It changes neither Actual nor Approved Budget by itself.

---
# 10. Planning Horizons

```text
Annual Budget
12/18/24-Month Rolling Forecast
3-Year Plan
5-Year Strategic Plan
```

---
# 11. Granularity By Horizon

Recommended: near term monthly; medium term monthly/quarterly; long term quarterly/annual. Do not force monthly entry for a five-year plan without business value.

---
# 12. Fiscal Calendar

Use Legal Entity fiscal periods where possible. Support 12-month, 13-period and custom fiscal-year starts. Planning periods must not silently diverge from Accounting periods.

---
# 13. Planning Cycle

`PlanningCycle` represents one managed process, e.g. FY2027 Annual Budget, FY2027 Q2 Forecast or 2027–2031 Strategic Plan.

---
# 14. Planning Cycle Concept

```text
PlanningCycle { Id, Scope, CycleType, StartPeriod, EndPeriod, ActualCutoff?, State, Owner, DueAt? }
```

---
# 15. Planning Cycle States

```text
Draft → Open → Collecting → Review → Approved → Published → Closed → Archived
```

---
# 16. Approved Baseline Rule

Once published, Original Budget is immutable. Later changes are explicit Revisions, Transfers or Forecast versions.

---
# 17. Original Budget

The first approved budget remains forever available for original-baseline variance.

---
# 18. Current Budget

```text
Original Budget
+ Approved Revisions
+ Transfers In
- Transfers Out
= Current Budget
```

---
# 19. Budget Revision

A Revision changes approved authority/amount and may change the company total.

---
# 20. Budget Transfer

A Transfer reallocates budget between controlled buckets and does not change company total.

---
# 21. Revision != Transfer

Never represent a transfer as two silent cell edits.

---
# 22. Forecast Version

Each published forecast remains a reproducible version: `Forecast Apr`, `Forecast May`, `Forecast Jun`.

---
# 23. Rolling Forecast

Actual periods roll forward; future periods remain forecast and the horizon can extend automatically.

---
# 24. Actual Cutoff

Every forecast version declares `ActualThroughPeriod` so users know which periods are Actual versus Forecast.

---
# 25. Actual Snapshot

For performance, TODIR may materialize aggregated actual snapshots. They remain rebuildable and are never accounting source truth.

---
# 26. Actual Snapshot Provenance

Store `Source`, `Watermark`, `AsOf`, `MappingVersion`, `CurrencyBasis` and refresh state.

---
# 27. Actual Is Read-Only

No planning user can edit imported Actual cells.

---
# 28. Budget vs Actual

Core comparison: Budget, Actual, Variance and Variance %.

---
# 29. Forecast vs Budget

Core comparison: Approved Budget, Current Forecast and Forecast Variance.

---
# 30. Forecast Accuracy

Compare prior forecast to later actual using explicit metrics such as absolute error, percentage error and bias. Never show one vague accuracy number.

---
# 31. Planning Model

A `PlanningModel` is an opinionated planning domain: Financial, Revenue, OPEX, Workforce or CapEx.

---
# 32. No Generic Hypercube First

Do not begin by building an unlimited arbitrary EPM cube. Start with typed models and controlled dimensions.

---
# 33. Financial Plan Model

Plans Revenue, COGS, OPEX, working capital, CapEx effects and projected P&L/Balance Sheet/Cash Flow.

---
# 34. Revenue Plan Model

Can plan by units, price, product/category, branch, channel and customer segment when enabled.

---
# 35. OPEX Plan Model

Plans spending by legal entity, department/cost center, category and period.

---
# 36. Workforce Plan Model

Plans FTE/headcount, hires/exits and expected employment cost without creating Employees.

---
# 37. CapEx Plan Model

Plans investment requests, timing, funding and expected financial impact without creating Fixed Assets.

---
# 38. Controlled Dimensions

Core dimensions may include Legal Entity, Period, Metric, Department/Cost Center, Branch, Product, Project reference, Channel, Customer Segment, Currency, Scenario and Version.

---
# 39. Dimension Activation

Only dimensions enabled for a model appear. A simple budget may be `Account × Month` only.

---
# 40. Dimension Ownership

Planning references canonical dimension owners; it does not copy Product, Project, Legal Entity or Department master truth.

---
# 41. Member Snapshot

Historical plan preserves display code/name context even if source master is later renamed.

---
# 42. Inactive Member

Historical plans remain readable; new planning may block inactive members according to policy.

---
# 43. Custom Dimensions

Allow a limited controlled number of custom planning dimensions when justified; avoid unlimited EAV complexity.

---
# 44. Planning Metrics

Metrics can be financial amount, quantity, rate, percent, headcount, days or units.

---
# 45. Financial vs Operational

`Revenue EGP` is financial; `Units Sold` and `Headcount` are operational drivers. Keep them distinct.

---
# 46. Planning Assumption

Assumptions include Sales Growth, Inflation, FX, Salary Increase, Material Inflation, DSO, DPO and Inventory Days.

---
# 47. Assumption != Plan Value

`Salary Increase = 8%` is an assumption; `Payroll Plan = 24M` is a calculated/entered plan value.

---
# 48. Assumption Versioning

Assumptions are scoped by Scenario/Version/Period/Entity and historized.

---
# 49. Driver-Based Planning

Prefer explainable business drivers over manual financial cell entry when a stable causal relationship exists.

---
# 50. Revenue Driver

Example: `Revenue = Units × Average Selling Price`.

---
# 51. Payroll Driver

Example: `Planned Payroll = FTE × Average Monthly Cost × Active Months`.

---
# 52. OPEX Driver

Examples: `Rent = Area × RatePerSqM`, or `Marketing = Revenue × MarketingPct` only where that reflects company policy.

---
# 53. Manufacturing Driver Integration

Consume planned volume/current manufacturing cost projections from Manufacturing/Inventory; never rebuild BOM/MRP costing in FP&A.

---
# 54. Formula Engine

Use a deterministic constrained planning formula engine. Tenant SQL/C#/JavaScript/Python is forbidden.

---
# 55. Formula DSL

Initial functions may include arithmetic, IF, MIN/MAX, ROUND, LAG, PRIOR_PERIOD, SUM, AVERAGE, LOOKUP_DRIVER, FX_CONVERT and SPREAD.

---
# 56. Dependency DAG

Calculated metrics declare dependencies. Build and validate a directed acyclic graph before execution.

---
# 57. Formula Cycle

Reject cycles such as `A=B; B=C; C=A`.

---
# 58. Formula Version

Published plan identifies exact model/formula version used.

---
# 59. Calculation Run

Record model version, plan version, scenario, input watermarks, timestamps and state.

---
# 60. Deterministic Calculation

Same inputs + same model version must produce same outputs subject to explicit rounding/currency rules.

---
# 61. Calculation Trace

Every calculated value can answer `Why this number?` with source drivers and formula lineage.

---
# 62. Calculated Cell

Calculated cells are read-only unless an explicit override rule exists.

---
# 63. Manual Override

Override requires value, reason, actor, timestamp and scope, and is visibly marked as an override.

---
# 64. Scenario Workspace

A Scenario copies an approved/forecast basis into a safe what-if workspace.

---
# 65. Scenario Types

Potential: Base, Optimistic, Conservative, Stress, Personal What-If, Shared What-If, Board Scenario.

---
# 66. Private Scenario

Private sandboxes cannot affect shared approved plan until explicitly shared/adopted.

---
# 67. Scenario Adoption

`Scenario → Review → Draft Forecast/Budget Revision → Approval → Publish`; never direct overwrite.

---
# 68. Scenario Comparison

Compare Base/Upside/Downside across Revenue, Margin, Profit, Cash, Headcount and CapEx.

---
# 69. Regional Stress Scenarios

Examples: FX shock, imported-material inflation, sales decline, energy-cost increase, supply disruption, salary inflation or branch delay.

---
# 70. Goal Seek Later

Future capability can solve for a driver target, e.g. units required for 20% margin. Not Day 1.

---
# 71. Monte Carlo Later

Probabilistic simulation is advanced/evidence-based; deterministic scenario planning comes first.

---
# 72. Revenue Planning

Can range from simple monthly revenue to `Product × Branch × Channel × Volume × Price`.

---
# 73. Revenue Plan != Sales Order

Planning creates no fake quotations/orders/invoices.

---
# 74. Revenue Actual

Recognized revenue normally comes from Ledger/Accounting mapping, not Sales Order total.

---
# 75. Sales Operational Drivers

Orders, shipments and invoices can be displayed as operational drivers when useful, clearly separated from accounting revenue.

---
# 76. Revenue Seed Methods

Prior-year actual, current run rate, sales pipeline, manual target or driver assumptions. Source is always labeled.

---
# 77. Pipeline Planning

CRM pipeline may inform forecast; Opportunity remains CRM truth.

---
# 78. Price Volume Mix

Advanced variance decomposition can separate Price, Volume and Mix when detail supports it.

---
# 79. OPEX Planning

Plan rent, utilities, marketing, software, travel, professional services, insurance, maintenance, telecom and other categories.

---
# 80. OPEX Methods

Manual, Prior Year + %, Run Rate, Driver-Based, allocated, and later contract-based methods.

---
# 81. Zero-Based Budgeting

Optional method where planned spend is justified from zero rather than prior baseline.

---
# 82. Incremental Budgeting

Optional method using prior Actual/Budget × growth/inflation factor.

---
# 83. Budget Owner

Department/cost center can have responsible planner/owner.

---
# 84. Department Submission

`Open Task → Enter/Review → Comment → Submit → Finance Review`.

---
# 85. Top-Down Planning

Executives set company targets/envelopes/constraints and allocate downward.

---
# 86. Bottom-Up Planning

Departments submit detailed needs upward.

---
# 87. Hybrid Planning

Support top-down target + bottom-up proposal + gap analysis without overwriting either source.

---
# 88. Budget Gap

Example: Top-down OPEX target 30M, bottom-up requests 34M, gap 4M.

---
# 89. Spreading

Convert annual/quarterly totals to periods by an explicit spreading method.

---
# 90. Even Spread

`12M annual → 1M/month`.

---
# 91. Seasonality Spread

Weighted periods, e.g. higher Ramadan/Eid campaign months.

---
# 92. Prior-Year Pattern

Use historical monthly proportions to spread new annual amount.

---
# 93. Custom Weights

Custom period weights validate to 100%.

---
# 94. Spread Rounding

Residual rounding is assigned deterministically so totals remain exact.

---
# 95. Allocation

Redistribute planning values across dimensions using a configured driver.

---
# 96. Allocation Example

`HQ Rent 12M → departments by floor area`.

---
# 97. Allocation Drivers

Headcount, Revenue, Area, Usage, Equal or Manual Weight.

---
# 98. Allocation Version

Published allocations identify exact rule version.

---
# 99. Allocation Trace

Every allocation explains source, driver, weight and destination.

---
# 100. Allocation Is Not Journal

Planning allocations never create Ledger entries.

---
# 101. Workforce Planning Mission

Plan people capacity and expected employment cost without creating fake employees or exposing unnecessary salary detail.

---
# 102. Workforce Grain

Simple: Department × FTE × Avg Cost. Advanced: Role/Position × Location × Start Month × FTE × assumptions.

---
# 103. Planned Hire

A planned hire is not an HR Employee.

---
# 104. Planned Exit

A planned exit assumption does not terminate an Employee.

---
# 105. Existing Workforce Baseline

Import current/aggregated headcount from HR with source watermark.

---
# 106. Workforce Privacy

Prefer role/grade/department aggregates unless individual planning is explicitly authorized.

---
# 107. Individual Workforce Planning

Optional restricted capability only; not default.

---
# 108. Planned Compensation

Planning assumption only. Actual compensation/paycheck remains HR/Payroll truth.

---
# 109. Workforce Cost Components

Base pay, employer tax, benefits, bonus, allowances, insurance and recruiting/onboarding assumptions.

---
# 110. Hire Proration

A July planned hire incurs only applicable future-period cost.

---
# 111. Vacant Role

Plan a vacant role/FTE before a person exists.

---
# 112. Workforce Transfer

Planning transfer between departments does not mutate HR assignment.

---
# 113. Actual Headcount

Source from HR effective-dated Employment as-of date.

---
# 114. Actual Payroll Cost

Source from Payroll/Ledger according to explicit metric definition.

---
# 115. Workforce Output

Feeds projected payroll/OPEX, headcount trend and financial cash model.

---
# 116. Workforce Boundary

Skills/succession/LMS remain HR or future workforce capability; do not make FP&A an HR suite.

---
# 117. CapEx Planning Mission

Decide which investments to fund, when and why, before a real Asset or Purchase exists.

---
# 118. CapEx Plan Item

Stores name/category/entity/department/planned amount/currency/purchase period/in-service period/business justification/priority.

---
# 119. CapEx Plan != Asset

No Fixed Asset exists until real acquisition/capitalization in `22`.

---
# 120. CapEx Plan != Purchase Order

Budget approval for investment does not create supplier commitment.

---
# 121. CapEx Prioritization

Compare Strategic Priority, Mandatory/Regulatory need, Expected Benefit, Risk, Payback and Funding Need without requiring full investment appraisal for every SME.

---
# 122. CapEx Approval

Use Platform Approvals. Planning approval authorizes budget envelope only; procurement approval remains Purchasing-owned.

---
# 123. CapEx Funding

Planning may classify Internal Cash, Debt, Lease, Project Funding or Other. Advanced debt management belongs future Treasury capability.

---
# 124. Planned Depreciation

Forecast depreciation may be modeled from planned asset assumptions but never becomes Fixed Assets book depreciation.

---
# 125. Planned Depreciation Inputs

Potential: Asset Class, In-Service Date, Useful Life Assumption, Method Assumption and Residual Value.

---
# 126. Actual CapEx

Source from Projects CIP, Purchasing/Invoices and Fixed Assets additions according to configured definition.

---
# 127. CapEx Envelope vs Project Budget

Corporate investment envelope belongs `28`; detailed Project budget, commitment, ETC and EAC remain `23`.

---
# 128. Project Integration

`28` can aggregate Project Budget, Actual, Commitment, ETC and EAC without copying Project truth.

---
# 129. Project Budget Rule

`23 Projects` remains source of detailed Project Budget and Forecast. This is locked by existing architecture.

---
# 130. Manufacturing Boundary

`26` plans what to manufacture/buy/transfer; `28` plans financial impact and management targets.

---
# 131. Inventory Boundary

Inventory owns actual stock/value. `28` may plan inventory days, target balances and working-capital assumptions only.

---
# 132. Working Capital Planning

Core drivers may include DSO, DPO and Inventory Days combined with Revenue/COGS/Purchases.

---
# 133. Receivables Forecast

Example driver: `Projected AR ≈ Revenue × DSO / days basis`; exact method is versioned and explicit.

---
# 134. Payables Forecast

Example driver: `Projected AP ≈ Purchases/COGS basis × DPO / days basis`.

---
# 135. Inventory Forecast

Example driver: `Projected Inventory ≈ COGS × InventoryDays / days basis` when appropriate.

---
# 136. Working Capital Assumption Rule

DSO/DPO/Inventory Days are planning assumptions; they do not mutate Subledger or Inventory.

---
# 137. Integrated Financial Planning

TODIR supports connected projected P&L, Balance Sheet and Cash Flow when the model is enabled.

---
# 138. Projected P&L

May combine planned Revenue, COGS, OPEX, depreciation, other income/expense and tax assumptions.

---
# 139. Projected Balance Sheet

May derive from opening actual balances, projected working capital, CapEx/depreciation, financing assumptions and retained earnings.

---
# 140. Projected Cash Flow

A financial planning statement derived from plan movements; not the bank-by-bank liquidity forecast in `25`.

---
# 141. Financial Forecast != Treasury Forecast

`28` = strategic/financial forecast. `25` = operational short-term liquidity forecast by Treasury Account and real/open cash events.

---
# 142. Three-Statement Integrity

If integrated statements are enabled, projected Balance Sheet must balance and projected ending cash must reconcile to projected Cash Flow.

---
# 143. No Fake Balance

Never hide unexplained difference using an unlabeled plug. Any balancing assumption is explicit, configured and visible.

---
# 144. Statement Mapping

Reuse Ledger/Accounting financial statement roles/mappings; do not create a second chart of accounts.

---
# 145. Planning Account Mapping

Versioned mapping can connect Ledger account/semantic role to planning metrics and statement lines.

---
# 146. Actual Financial Statements

Actual P&L/Balance Sheet remain Accounting-owned. `28` shows read-only comparisons.

---
# 147. Planning Tax

Forecast tax can use explicit planning assumptions; actual statutory calculation belongs Tax/Country/Accounting.

---
# 148. Planning Interest

Forecast interest/debt assumptions are planning-only; advanced debt management remains future Treasury.

---
# 149. Multi-Currency Planning

Support Entity Local Currency, driver/transaction currency where needed, and Group Reporting Currency.

---
# 150. Planning FX

Planning FX rates are scenario assumptions and are distinct from actual FinBase transaction FX snapshots.

---
# 151. Planning FX Rate

```text
PlanningFxRate { Scenario, Version, Period, FromCurrency, ToCurrency, RateType, Rate }
```

---
# 152. Planning FX Rate Types

Potential: Average, Closing, BudgetRate, Custom.

---
# 153. P&L Translation

Planning translation typically uses average-rate policy; configure explicitly.

---
# 154. Balance Sheet Translation

Planning balance-sheet translation typically uses closing-rate policy; configure explicitly.

---
# 155. FX Scenario

Base USD/EGP 55 vs Stress 70 can show impact on imported cost, margin, cash and payables.

---
# 156. Multi-Entity Planning

Plan each Legal Entity independently, then roll up authorized management views.

---
# 157. Group Planning Rollup

Aggregate entities to management reporting currency with explicit conversion basis.

---
# 158. Planning Rollup != Statutory Consolidation

Display this distinction clearly. Legal consolidation/elimination remains future/accounting capability.

---
# 159. Intercompany Planning

Architecture seam may support intercompany planning and management eliminations later. Not Day 1.

---
# 160. Budget Control Mission

> **Protect approved spending authority at transaction time without duplicating the approval workflow of Purchasing, Expenses or Projects.**

---
# 161. Budget Control Modes

```text
None
Warn
RequireApproval
Block
```

---
# 162. Budget Control Basis

Policy can include Actual only; Actual+Commitment; Actual+Precommitment+Commitment; or Actual+OpenCommitment+Proposed.

---
# 163. Recommended Purchasing Basis

For strict purchase control: `Actual + Open Commitment + Proposed Commitment`, optionally including approved PR precommitments.

---
# 164. Budget Check Contract

Representative result: `Allow | Warn | RequireApproval | Block`, with reasons, available funds and control bucket.

---
# 165. Budget Check Is Not Approval

`Allow` means budget is available; owning module still performs its own business approval.

---
# 166. Budget Control Scope

Can control by Legal Entity, Fiscal Year/Period, Department/Cost Center, Budget Category/Account, Project envelope and Branch where configured.

---
# 167. Budget Control Bucket

A bucket is the exact combination where funds are controlled, e.g. FY2027 + Egypt LLC + Marketing + Advertising.

---
# 168. Budget Availability Formula

Base concept: `Current Approved Budget - Actual - Active Precommitments - Active Commitments - Active Reservations = Available Before Proposed`, with policy-specific inclusion.

---
# 169. Proposed Transaction

Preview can test ProposedAmount without persisting reservation.

---
# 170. Preview != Reserve

`CheckBudgetPreview` and `CheckAndReserveBudget` are separate semantics.

---
# 171. Budget Reservation

At configured source state, create budget reservation/commitment transactionally.

---
# 172. Budget Concurrency

Two concurrent purchase approvals cannot both consume the same remaining funds. Use transactional check-and-reserve and row/version locking.

---
# 173. Strict Failure Mode

If control mode is `Block` and budget service/control projection is unhealthy, fail closed.

---
# 174. Advisory Failure Mode

Warn-only policies may allow configured degraded behavior, but never silently bypass control.

---
# 175. Precommitment

Typical source: approved Purchase Requisition or approved internal spending request, if precommitment tracking is enabled.

---
# 176. Commitment

Typical source: approved Purchase Order or other legally/commercially committed future spend.

---
# 177. Actual Budget Consumption

Actual consumption comes from canonical posted/recognized source mapping, normally Ledger/Accounting facts.

---
# 178. PR To PO Chain

When PR becomes PO, release/replace PR precommitment and create PO commitment without double count.

---
# 179. PO To Actual Chain

When invoice/actual is recognized, reduce related open commitment and let Actual take over.

---
# 180. No Double Consumption

PR + PO + Invoice must never all count fully for the same economic amount.

---
# 181. Commitment Lineage

Track source chain `Request → Order → Invoice/Actual → Correction/Cancel`.

---
# 182. Cancellation Release

Cancelled source commitment releases remaining budget authority idempotently.

---
# 183. Partial Invoice

Partial actual consumption reduces only corresponding commitment amount; remaining PO commitment stays open.

---
# 184. PO Change

PO increase checks/reserves additional budget. PO decrease releases excess commitment.

---
# 185. PO Currency Change

Re-evaluate control amount using configured budget FX basis; never silently keep stale amount.

---
# 186. Budget Control Currency

Each control bucket has explicit currency. Cross-currency source transactions use planning/control FX policy.

---
# 187. Budget FX For Control

Budget-control conversion rate is versioned and distinct from invoice/payment actual FX when appropriate.

---
# 188. Expense Claim Control

Expenses can call Budget Check before approval/payment according to policy; `21 Expenses` remains expense owner.

---
# 189. Project Purchase Control

A project-specific purchase may need both Project Budget check and Corporate Budget check only if company policy explicitly enables both; avoid accidental double governance.

---
# 190. Control Precedence

Define which budget owner is authoritative for each source context: Corporate, Department, Project envelope, or other. Never check every budget blindly.

---
# 191. Budget Override

Override is typed exception with amount, reason, approver, bucket, source transaction and timestamp.

---
# 192. Override != Budget Revision

An override permits one transaction; it does not permanently increase approved budget.

---
# 193. Budget Reservation State

Potential states: Active, PartiallyConsumed, Consumed, Released, Cancelled, Corrected.

---
# 194. Reservation Idempotency

Retrying source approval must not reserve twice.

---
# 195. Budget Consumption Correction

Source corrections/reversals create corresponding control adjustments; never edit consumed totals directly.

---
# 196. Budget Availability Projection

Maintain fast transactional/rebuildable balance projection per control bucket.

---
# 197. Projection Components

Store CurrentBudget, Actual, Precommitment, Commitment, Reservation, Available and AsOf/Version.

---
# 198. Control Projection Freshness

Strict budget checks require current-enough transactional projection; nightly FP&A snapshots are insufficient.

---
# 199. Budget Control vs FP&A Actual Snapshot

Keep separate: operational budget-control projection is near-real-time; planning Actual snapshot can be periodic/rebuildable.

---
# 200. Budget Control Period

Control can be annual, quarterly or monthly. Policy defines whether unused amount carries forward.

---
# 201. Carryforward

Potential: None, WithinFiscalYear, ExplicitTransfer, YearEndCarryforward. Must be explicit.

---
# 202. Period Overrun

Monthly overrun can optionally consume annual bucket only if policy allows; do not infer.

---
# 203. Control Threshold

Can warn/require approval at usage thresholds, e.g. 80%, 95%, 100%.

---
# 204. Threshold Calculation

Threshold is based on configured denominator/current approved budget and inclusion basis.

---
# 205. Negative Budget

Allowed only by explicit policy/adjustment. Never emerge from hidden rounding or uncontrolled overspend.

---
# 206. Revenue Budget Control

Revenue plans are normally monitored rather than transaction-blocked. Keep expenditure control first-class and revenue control optional.

---
# 207. CapEx Budget Control

Approved CapEx envelope can be checked when procurement/project commitment is created.

---
# 208. Budget Transfer Workflow

`Draft Transfer → Validate Funds → Approval → Apply → Audit`.

---
# 209. Transfer Conservation

Within same currency/scope, total Transfer Out equals Transfer In, subject to explicit FX/rounding policy for cross-currency cases.

---
# 210. Transfer Restrictions

Policy may prevent transfer from restricted/mandatory budgets or across Legal Entities.

---
# 211. Cross-Entity Transfer

A corporate planning reallocation across entities is a planning governance action, not an intercompany accounting transaction.

---
# 212. Budget Revision Workflow

`Draft Revision → Justification → Approval → Publish`.

---
# 213. Revision Effective Period

Revision declares effective period; prior-period historical comparisons remain intact.

---
# 214. Budget Freeze

After period close or management freeze, direct edits are blocked; revisions remain explicit.

---
# 215. Budget Workflow

Small company: `Create → Review → Approve`. Larger: department tasks → submit → Finance review → CFO approval → publish.

---
# 216. Platform Approvals

Use shared Platform Approvals; do not build a second approval engine.

---
# 217. Planner Assignment

Assign responsibility by dimension member: department, entity, branch or model slice.

---
# 218. Submission Unit

A submission unit defines which slice a planner submits/locks, e.g. Marketing Department FY2027 OPEX.

---
# 219. Submission State

Draft, Ready, Submitted, Returned, Approved, Published.

---
# 220. Submit Lock

Submitted slice becomes read-only to planner until returned/unlocked under workflow.

---
# 221. Finance Adjustment

Finance can either edit under authorized policy or return to owner; every change keeps actor/history.

---
# 222. Manager Commentary

Comments attach to model/version/period/dimension coordinate, not floating text.

---
# 223. Comment Types

Assumption, VarianceExplanation, ReviewNote, ApprovalNote, Risk, Action.

---
# 224. Attachments

Use Platform Files for supporting budget documents.

---
# 225. Cell History

For editable input values record prior value, new value, actor, timestamp, reason/comment where required.

---
# 226. No Audit Flood UX

Full history is available, but normal grid shows concise latest context.

---
# 227. Spreadsheet-Like UX

Planning entry must support familiar grid interactions rather than forcing one form per month/account.

---
# 228. Grid Features

Required: keyboard navigation, copy/paste, multi-cell paste, fill right/down, percentage increase, spread, comments, bulk clear, undo within draft session where feasible.

---
# 229. Excel Paste

Paste tabular data with validation preview and cell-level error feedback.

---
# 230. Excel Import

Import Excel/CSV with mapping, preview, duplicate handling and validation before commit.

---
# 231. Excel Export

Export current view/version with enough metadata for safe re-import where supported.

---
# 232. No Excel As Source Of Truth

Excel is an interaction/import surface. Approved TODIR version is authoritative planning record.

---
# 233. Grid Virtualization

Large grids must virtualize rows/columns; never render millions of DOM cells.

---
# 234. Bulk Write

Use batch cell command endpoints; never one HTTP request per cell.

---
# 235. Paste Transaction

Bulk paste validates all cells and supports atomic or clearly reported partial policy. Prefer atomic per submission batch for critical plan slices.

---
# 236. Draft Session

Planner can work in a private/draft version before publish/submit.

---
# 237. Conflict Handling

Optimistic concurrency detects edits to same version/slice. Never last-write-wins silently.

---
# 238. Value Lock

Authorized user can lock selected cells/rows/periods against allocation/spread overwrite.

---
# 239. Calculated Cell Style

Calculated, imported Actual, entered input, overridden and allocated values are visually distinct.

---
# 240. Plan Status Visibility

Grid shows dirty/recalculation state and last calculation time.

---
# 241. Recalculation

Small local changes may recalc synchronously; large model recalculation runs job with progress.

---
# 242. Stale Result

If dependent calculation is stale, UI labels it. Never present stale number as current without warning.

---
# 243. Owner Dashboard

Owner-friendly labels: Expected Revenue, Expected Profit, Expected Cash, Spend vs Plan, Hiring Plan, Investment Plan, Risks.

---
# 244. FP&A Workspace

Finance-facing: Versions, Scenarios, Drivers, Allocations, Forecast, Variance, Submission Status, Budget Control.

---
# 245. Department Manager Workspace

Shows own budget, Actual, commitments, available budget, forecast and required actions without irrelevant finance administration.

---
# 246. CFO Workspace

Shows group/entity plan, forecast, cash, margin, CapEx, headcount, major variances, risks and forecast accuracy.

---
# 247. Variance Analysis

Core mission: explain not only that performance differs from plan, but why.

---
# 248. Variance Sign Convention

Document favorable/unfavorable display by metric; do not use one sign convention blindly for Revenue and Expense.

---
# 249. Revenue Variance

Can decompose Price/Volume/Mix if driver detail exists.

---
# 250. Payroll Variance

Can decompose Headcount, Start-Date, Rate and Bonus/Benefit effects where model supports.

---
# 251. OPEX Variance

Can show volume/driver change, rate/inflation, timing and one-off spend.

---
# 252. CapEx Variance

Can show timing shift, cancellation, amount over/under, delayed in-service.

---
# 253. FX Variance

Can isolate planning FX assumption effect from local-currency operating variance when data supports.

---
# 254. Variance Commentary

Finance/manager records explanation tied to exact period/version/metric/dimension.

---
# 255. Variance Threshold

Require commentary only when materiality threshold is exceeded to avoid noise.

---
# 256. Materiality

Can be amount, percentage or both; model-specific.

---
# 257. Management Action

Variance can create action/task via Platform task/notification seam, not hidden note only.

---
# 258. Forecast Methods

Potential per line/metric: Manual, RunRate, PriorYear, BudgetRemaining, DriverBased, SourceFeed, PredictiveLater.

---
# 259. Run-Rate Forecast

Use YTD average/trend under explicit method; show formula.

---
# 260. Prior-Year Forecast

Uses prior-year periods with growth/seasonality assumptions.

---
# 261. Budget Remaining Forecast

Can seed future forecast from approved budget while actual periods replace budget in the full-year view.

---
# 262. Source Feed Forecast

Can consume Sales pipeline, Project EAC, Payroll plan, MRP cost or other owner forecasts through contracts.

---
# 263. No Silent Forecast Method Change

Changing forecast method creates visible version/model change.

---
# 264. Forecast Override

Planner override is explicit and preserves model-generated suggestion for comparison.

---
# 265. Forecast Approval

Published Forecast can require Finance/CFO approval depending company policy.

---
# 266. Forecast Is Not Accounting

Never creates Journal Entry, Invoice, Payment or Asset.

---
# 267. Long-Range Plan

Strategic plan supports fewer details and longer horizons; it can set targets and major drivers without annual-budget line-level burden.

---
# 268. Strategic Targets

Examples: Revenue CAGR, Margin, Headcount, Branch count, CapEx, Debt ratio later, Cash reserve.

---
# 269. Strategic Plan To Annual Budget

Annual budget may be seeded from strategic targets but remains separate approved version.

---
# 270. Target Reconciliation

Show gap between strategic target and submitted annual plan.

---
# 271. Actual Mapping

Actuals are aggregated through versioned mapping from canonical source dimensions/accounts into planning metrics.

---
# 272. Actual Mapping Change

A mapping revision must not silently change previously published management report without recalculation/version disclosure.

---
# 273. Actual Restatement

If accounting Actual is corrected, planning Actual snapshot can refresh; published forecast comparisons preserve source watermarks or show restatement status.

---
# 274. Close-Aware Actual

Forecast can use posted-to-date or closed-period Actual based on policy. Label basis.

---
# 275. Unposted Actual

Do not treat draft/unposted accounting data as final Actual unless explicitly configured as preliminary.

---
# 276. Commitment Data

Open commitments are source-linked and distinct from Actual.

---
# 277. Available Budget Dashboard

For controlled bucket show Current Budget, Actual, Precommitment, Commitment, Proposed, Available and utilization %.

---
# 278. Budget Drilldown

Every consumption amount drills to source document/fact.

---
# 279. Commitment Aging

Show old/stale commitments that may need closure/release by owning module.

---
# 280. Commitment Staleness

FP&A cannot cancel stale PO itself; it flags owning module/user.

---
# 281. Budget Health

Finance home shows overspent buckets, near-threshold buckets, stale commitments, pending transfers/revisions and control errors.

---
# 282. Noisy Health Rule

Healthy budgets should not generate task flood.

---
# 283. Budget Utilization

`Consumed / Current Budget` with explicit consumed basis.

---
# 284. Available vs Uncommitted

Use clear labels. Available can mean budget available after actual+commitments; Uncommitted may exclude actual depending definition. Do not conflate.

---
# 285. Forecast vs Available Budget

A forecast can exceed current approved budget; this is a management warning, not silent budget increase.

---
# 286. Forecast Overrun

Show expected overrun early even before transaction-level budget block occurs.

---
# 287. Budget At Completion

For corporate cost area, can compare Current Budget vs Forecast Full-Year; not Project EAC unless sourced from Projects.

---
# 288. Scenario Security

Private scenarios visible only to owner/authorized users until shared.

---
# 289. Sensitive Workforce Security

Headcount aggregate may be broad; individual compensation assumptions are restricted.

---
# 290. Strategic Scenario Security

M&A, restructuring or confidential expansion scenarios can be restricted to named planners/execs.

---
# 291. Planning Role

Potential roles: Planner, BudgetOwner, Reviewer, FP&AAdmin, CFO, ReadOnlyExecutive, BudgetController.

---
# 292. Planner Scope

Scope by entity/model/department/branch/version/scenario.

---
# 293. Budget Controller

Can inspect control buckets and exceptions without automatically gaining all confidential scenarios.

---
# 294. FP&A Admin

Manages models/mappings/cycles but may not approve own budget by SoD policy.

---
# 295. CFO

Can approve/publish authorized plans and view group management plan subject to entity access.

---
# 296. SoD

Optional: Model Admin != Final Approver; Budget Transfer Maker != Approver; Override Requester != Approver.

---
# 297. Small Company SoD

Do not hardcode enterprise maker-checker for tiny companies.

---
# 298. Planning Export Security

Exports respect row/dimension/sensitive-field permissions.

---
# 299. Actual Drillback Security

Planner can drill only to source transactions they are authorized to see.

---
# 300. No Permission Escalation Through Planning

A Budget grid must not become a shortcut to Payroll salaries, supplier price details or restricted ledger entries.

---
# 301. Audit

Audit cycle creation, model/version changes, input edits, submissions, approvals, revisions, transfers, overrides, formula/mapping changes, budget reservations/releases and publication.

---
# 302. Audit Value Protection

Sensitive planning values follow security policy; audit does not expose secret data to unauthorized readers.

---
# 303. Comment Audit

Material comments/variance explanations preserve author/time/history.

---
# 304. Version Compare

Compare any two plan versions and identify changed inputs/assumptions/calculated outputs.

---
# 305. Version Diff

Support diff by value, percent, driver and dimension scope.

---
# 306. Assumption Diff

Scenario compare should highlight assumption changes before financial outputs.

---
# 307. Published Version Immutability

Published version cannot be edited. Corrections create new version.

---
# 308. Archive

Closed old cycles remain queryable under retention policy.

---
# 309. Delete Draft

Unpublished personal/draft scenario may be deletable under policy; published history never deleted through ordinary UI.

---
# 310. Planning Notes

Notes/comments do not replace structured assumption data.

---
# 311. Currency Precision

Use FinBase decimal/rounding conventions. Never float.

---
# 312. Percentage Precision

Store adequate decimal precision; display configurable.

---
# 313. Headcount Precision

FTE can be decimal; people count may be integer depending metric.

---
# 314. Null vs Zero

Unentered/null means no input; zero is an explicit value. Preserve distinction.

---
# 315. Zero Suppression

UI may hide zero rows but storage/calculation semantics remain explicit.

---
# 316. Missing Input

Calculated models can flag required missing drivers instead of treating missing as zero silently.

---
# 317. Input Validation

Range, UOM, currency, dimension membership, period and permission validation server-side.

---
# 318. Cell Provenance

Every cell can identify SourceType: Entered, Calculated, ImportedActual, Allocated, Spread, Override, SourceFeed.

---
# 319. Source Watermark

Imported/source-fed planning values retain upstream watermark/version when available.

---
# 320. Lineage

Executive number must drill to plan lines/drivers and Actual source summary without exposing unauthorized detail.

---
# 321. Recommended Schema

```text
business_planning
```

---
# 322. PlanningDbContext

Owns Planning/FP&A tables only.

---
# 323. Core Tables — Cycle & Model

Potential:
```text
planning_cycle
planning_model
planning_model_version
planning_model_dimension
planning_metric
planning_metric_mapping
planning_formula
planning_formula_dependency
```

---
# 324. Core Tables — Versions

Potential:
```text
plan_version
scenario
submission_unit
planning_comment
planning_attachment_ref
```

---
# 325. Core Tables — Facts

Potential:
```text
planning_coordinate
planning_coordinate_member
planning_input_fact
planning_calculated_fact
planning_actual_snapshot_fact
planning_source_fact
```

---
# 326. Core Tables — Drivers

Potential:
```text
planning_assumption
planning_assumption_value
spread_rule
allocation_rule
allocation_run
```

---
# 327. Core Tables — Budget

Potential:
```text
budget_baseline
budget_revision
budget_transfer
budget_control_policy
budget_control_bucket
budget_reservation
budget_consumption_fact
budget_balance_projection
budget_override
```

---
# 328. Core Tables — Specialized Plans

Potential:
```text
workforce_plan_line
capex_plan_item
revenue_plan_driver
working_capital_assumption
planning_fx_rate
```

---
# 329. Core Tables — Jobs/Trace

Potential:
```text
planning_calculation_run
planning_calculation_trace
actual_refresh_run
forecast_accuracy_projection
variance_projection
```

---
# 330. Tables 28 Must Not Own

Forbidden duplicate truth:
```text
ledger_account
journal_entry
open_item
payment
bank_account
project_budget
project_forecast
employee
payslip
fixed_asset
purchase_order
sales_order
inventory_movement
production_order
```

---
# 331. Planning Coordinate Strategy

Use sparse coordinates. A coordinate identifies one combination of active model dimensions plus period/metric/version.

---
# 332. No Naive EAV

Do not model every number as untyped name/value rows. Dimension definitions and member kinds are typed and validated.

---
# 333. Coordinate Hash

Store deterministic coordinate signature/hash to deduplicate coordinates and support fast upsert/lookups.

---
# 334. Coordinate Membership

A coordinate links only active dimensions for its model. Unique constraint prevents duplicate member for same dimension.

---
# 335. Input Fact

Represents editable source input at one coordinate.

---
# 336. Calculated Fact

Rebuildable output from formula/calculation run.

---
# 337. Actual Snapshot Fact

Rebuildable read-only aggregate from canonical Actual sources.

---
# 338. Source Feed Fact

Rebuildable/read-only operational forecast input, e.g. Project EAC or Sales pipeline projection.

---
# 339. Sparse Storage

Unentered cells are not materialized as millions of zeros.

---
# 340. Zero != Missing

Explicit zero can be stored because it may intentionally override seed/driver logic.

---
# 341. Model Version

Dimension set, formulas, metric definitions and rules are versioned together when material.

---
# 342. Version Freeze

Published plan references immutable ModelVersion.

---
# 343. RLS

All tenant-owned tables use PostgreSQL `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY`.

---
# 344. Runtime Role

No DB ownership/BYPASSRLS for normal runtime.

---
# 345. Legal Entity Scope

Every financial plan fact has explicit entity scope or approved group scope; no hidden cross-entity assumption.

---
# 346. Group Scenario Scope

A group scenario can reference multiple entities but authorization remains entity-aware.

---
# 347. IDs

UUIDv7 for domain records.

---
# 348. Money

FinBase decimal/Money semantics; never float.

---
# 349. Time

NodaTime; fiscal period identity is explicit.

---
# 350. Optimistic Concurrency

Use for draft plan versions, submissions, revisions, transfers and model edits.

---
# 351. Calculation Locks

Prevent two incompatible publishes/recalculations from racing on same version/scope.

---
# 352. Job Idempotency

Re-running Actual refresh, calculation, spread, allocation or publication job must be safe.

---
# 353. Outbox

Use Platform Outbox for asynchronous source/projection notifications.

---
# 354. RabbitMQ

Async integration only; never substitute for atomic strict Budget Check/Reserve when transaction invariant requires sync.

---
# 355. Transaction Coordinator

Use approved Platform Transaction Coordinator for same-database cross-module atomic budget reservation + source approval when required.

---
# 356. Budget Check Atomicity

For strict commitment creation: owning transaction and Budget reservation must either both commit or both fail under approved integration pattern.

---
# 357. Budget Control Idempotency Key

Use source module + source document/line + source version to avoid duplicate reservation.

---
# 358. Source Correction Idempotency

Reversal/correction references original consumption fact and cannot apply twice.

---
# 359. Planning APIs — Cycles

Representative:
```text
GET/POST /planning/cycles
POST /planning/cycles/{id}/open
POST /planning/cycles/{id}/submit
POST /planning/cycles/{id}/approve
POST /planning/cycles/{id}/publish
```

---
# 360. Planning APIs — Versions

```text
GET /planning/versions
POST /planning/versions
POST /planning/versions/{id}/clone
GET /planning/versions/{id}/compare
POST /planning/versions/{id}/recalculate
```

---
# 361. Planning APIs — Grid

```text
POST /planning/grids/query
POST /planning/grids/write-batch
POST /planning/grids/spread
POST /planning/grids/paste-preview
```

---
# 362. Planning APIs — Scenario

```text
POST /planning/scenarios
POST /planning/scenarios/{id}/clone
POST /planning/scenarios/{id}/share
POST /planning/scenarios/{id}/adopt-preview
```

---
# 363. Planning APIs — Assumptions

```text
GET /planning/assumptions
POST /planning/assumptions
POST /planning/assumptions/{id}/values
```

---
# 364. Planning APIs — Budget Control

```text
POST /planning/budget/check
POST /planning/budget/check-and-reserve
POST /planning/budget/reservations/{id}/adjust
POST /planning/budget/reservations/{id}/release
GET  /planning/budget/buckets/{id}/availability
```

---
# 365. Budget Check Response

Must include:
```text
Decision
ControlBucket
CurrentBudget
Actual
Commitment
Proposed
AvailableBefore
AvailableAfter
ThresholdStatus
Reasons[]
```

---
# 366. Planning APIs — Revision/Transfer

```text
POST /planning/budgets/{id}/revisions
POST /planning/budgets/{id}/transfers
POST /planning/budget-transfers/{id}/approve
```

---
# 367. Planning APIs — Workforce

```text
GET/POST /planning/workforce
POST /planning/workforce/import-baseline
GET /planning/workforce/cost-impact
```

---
# 368. Planning APIs — CapEx

```text
GET/POST /planning/capex
POST /planning/capex/{id}/prioritize
POST /planning/capex/{id}/approve-budget
```

---
# 369. Planning APIs — Actual Refresh

```text
POST /planning/actuals/refresh
GET /planning/actuals/refresh-runs/{id}
```

---
# 370. Planning APIs — Variance

```text
POST /planning/variance/query
GET /planning/variance/{coordinate}/drivers
POST /planning/variance/{coordinate}/commentary
```

---
# 371. Commands — Cycle

CreatePlanningCycle, OpenCycle, CloseCycle, ArchiveCycle.

---
# 372. Commands — Version

CreatePlanVersion, CloneVersion, SubmitVersionSlice, ReturnSubmission, ApproveVersion, PublishVersion.

---
# 373. Commands — Input

WritePlanningInputBatch, ClearPlanningInputBatch, ApplySpread, RunAllocation.

---
# 374. Commands — Budget

CreateBudgetBaseline, CreateBudgetRevision, CreateBudgetTransfer, ApproveBudgetTransfer, RequestBudgetOverride.

---
# 375. Commands — Control

CheckBudget, ReserveBudget, AdjustReservation, ReleaseReservation, ApplyConsumptionCorrection.

---
# 376. Commands — Scenario

CreateScenario, ChangeScenarioAssumptions, ShareScenario, CreateForecastFromScenario.

---
# 377. No Generic SetActual

Forbidden.

---
# 378. No Generic SetAvailableBudget

Forbidden. Available is derived.

---
# 379. No Generic SetForecastTotal

If forecast is driver/calculated, use explicit input/override semantics.

---
# 380. No Generic SetBudgetConsumed

Consumed amount derives from source-linked facts.

---
# 381. Contracts

Recommended: `Todir.Planning.Contracts`.

---
# 382. Namespaces

```text
Todir.Modules.Planning.Domain
Todir.Modules.Planning.Application
Todir.Modules.Planning.Infrastructure
Todir.Planning.Contracts
```

---
# 383. Internal Areas

Potential:
```text
Cycles/ Models/ Versions/ Grid/ Assumptions/ Calculations/ Scenarios/ Budgeting/ BudgetControl/ Revenue/ Opex/ Workforce/ CapEx/ Actuals/ Variance/ Statements/ Reporting/
```

---
# 384. Source Contract — Ledger

Need period/account/dimension aggregate Actual contract and financial statement mapping references.

---
# 385. Source Contract — Projects

Need Project Budget/Actual/Commitment/ETC/EAC aggregates; no direct Project DB access.

---
# 386. Source Contract — Banking

Need Treasury short-term forecast/cash-position summary only when management comparison requires it.

---
# 387. Source Contract — HR

Need authorized headcount/role/department aggregates and optional restricted workforce baseline.

---
# 388. Source Contract — Payroll

Need authorized aggregate actual payroll cost by planning dimensions; never broad employee payslip exposure.

---
# 389. Source Contract — Purchasing

Need precommitment/commitment lifecycle and budget-control check/reservation integration.

---
# 390. Source Contract — Expenses

Need expense commitment/actual source facts for budget-control/variance integration.

---
# 391. Source Contract — Sales

Need operational revenue drivers/pipeline/order summaries only as defined.

---
# 392. Source Contract — Inventory

Need actual inventory value/cost and planning driver summaries; no stock mutation.

---
# 393. Source Contract — Manufacturing

Need planned production/current cost projections where used in driver models; MRP stays Manufacturing-owned.

---
# 394. Source Contract — Fixed Assets

Need actual additions/depreciation summary for Actual/variance; plan remains Planning-owned.

---
# 395. Source Contract — FinBase

Currencies, decimals, rounding and actual FX references.

---
# 396. Events Consumed

Potential targeted events: PostedFinancialPeriodChanged, PurchaseCommitmentChanged, ProjectForecastPublished, PayrollRunFinalized, InventoryValuationChanged, FixedAssetBookPosted.

---
# 397. Do Not Subscribe To Everything

Prefer source contracts/batched refresh for high-volume planning Actuals; consume events only where timely incremental update has value.

---
# 398. Events Emitted

Potential: BudgetPublishedV1, BudgetControlReservationChangedV1, ForecastPublishedV1, BudgetTransferApprovedV1, BudgetRevisionApprovedV1.

---
# 399. Budget Control Consumer Contract

Purchasing/Expenses call synchronous `CheckBudget` / `CheckAndReserveBudget`; async event alone is insufficient for strict block invariant.

---
# 400. Performance Mission

Spreadsheet-fast planning for humans; safe batch calculation for models; transaction-safe budget control.

---
# 401. Grid Open Target

For normal slice:
```text
p95 <= 200-500ms
```
with virtualization and pre-aggregated view.

---
# 402. Budget Availability Target

Strict transaction check:
```text
p95 <= 100-250ms
```
under realistic tenant contention, excluding owning workflow latency.

---
# 403. Bulk Paste Target

Thousands of cells validate/write in one batch with bounded response/job behavior; never per-cell HTTP.

---
# 404. Scenario Recalc Target

Small/medium models should feel interactive; large model recalculation can be job-based. Publish measured limits rather than promising instant all-scale recalculation.

---
# 405. CFO Dashboard Target

```text
p95 <= 250-600ms
```
using summary projections.

---
# 406. Planning Fact Scale

Test tens of millions of sparse planning facts/coordinates across platform.

---
# 407. No N+1

Grid/dashboard cannot query each department/account/period separately.

---
# 408. Set-Based Aggregation

Use PostgreSQL set operations/materialized projections for actual snapshots, variance and budget balances.

---
# 409. Calculation Graph Cache

Compiled model/formula graph may be cached in application memory as rebuildable metadata; database remains truth.

---
# 410. No Redis Day 1

Do not add Redis solely for planning unless measured need justifies ADR.

---
# 411. No OLAP Database Day 1

PostgreSQL-first. Do not introduce ClickHouse/Essbase-like dedicated cube engine before evidence.

---
# 412. No OpenSearch Day 1

Not required for core planning.

---
# 413. Projection Rebuild

All derived projections have semantic rebuild procedure from canonical planning/source facts.

---
# 414. Incremental Actual Refresh

Refresh only affected periods/dimensions where source provides watermark/delta.

---
# 415. Large Actual Reload

Run background job with staging/validation/swap pattern; do not corrupt current published snapshot mid-refresh.

---
# 416. Calculation Batch

Group changed coordinates by dependency impact and recalc affected graph, not every cell in every model.

---
# 417. Dependency Impact

Changing one assumption recalculates only dependent metrics/slices where possible.

---
# 418. Scenario Isolation

Private what-if calculations must not lock/pollute published version.

---
# 419. Budget Control Hot Rows

Design control bucket locking to minimize contention; use deterministic lock order for multi-bucket transaction.

---
# 420. Multi-Bucket Reservation

If one source line consumes multiple buckets, reserve atomically or fail as one decision according to policy.

---
# 421. Deadlock Avoidance

Sort bucket lock order and retry safe transient deadlocks.

---
# 422. Indexes — Planning Facts

Likely index around Tenant/Model/Version/Period/Metric/CoordinateHash and dimension memberships according to measured queries.

---
# 423. Indexes — Budget Control

Unique/control indexes on Tenant/Entity/Policy/Bucket/Period and source idempotency key.

---
# 424. Indexes — Workflow

Cycle/Version/SubmissionState/Owner/DueAt.

---
# 425. Partitioning

Not Day 1. Consider only after real fact-table scale evidence.

---
# 426. Compression/Archival

Old closed plan versions can use archival storage strategy later while preserving audit/read access.

---
# 427. Jobs

Potential: Actual Refresh, Recalc, Allocation, Spread, Rolling Forecast Extension, Variance Refresh, Forecast Accuracy, Large Export, Snapshot Publish.

---
# 428. Observability Metrics

Examples:
```text
planning_grid_query_duration
planning_recalc_duration
planning_recalc_cells
planning_actual_refresh_duration
planning_budget_check_duration
planning_budget_block_total
planning_override_total
planning_forecast_publish_total
planning_stale_projection_count
```

---
# 429. No High-Cardinality Labels

Do not label telemetry by PlanId/DepartmentId/UserId.

---
# 430. Trace — Budget Check

```text
Resolve Policy
Resolve Bucket
Load Current Balance
Resolve FX
Apply Proposed
Evaluate Threshold
Decision
Reserve if required
```

---
# 431. Trace — Grid Calculation

```text
Load Inputs
Resolve Assumptions
Evaluate Dependencies
Currency Conversion
Allocation/Spread if requested
Persist Projection
```

---
# 432. Trace — Actual Refresh

```text
Source Watermark
Load Aggregates
Map Dimensions/Metrics
Validate Totals
Publish Snapshot
```

---
# 433. Source Reconciliation

Actual planning snapshot total reconciles to configured source report/ledger aggregate before publish.

---
# 434. Budget Control Reconciliation

Budget Actual consumption must reconcile to mapped posted Actual plus explicit timing/exclusion differences.

---
# 435. Commitment Reconciliation

Open budget commitments reconcile to owning-source open commitment facts.

---
# 436. Planning Statement Reconciliation

Projected statements reconcile according to formula model; actual comparison reconciles to Accounting.

---
# 437. Currency Reconciliation

Local and reporting currency totals reconcile to versioned planning FX conversion, including rounding.

---
# 438. Forecast Accuracy Reconciliation

Prior forecast snapshot remains fixed so later actual comparison is reproducible.

---
# 439. Import / Migration

Support migration from Excel, legacy budgeting tools and prior ERP budgets.

---
# 440. Migration — Approved Budget

Import current approved baseline with source, fiscal period, dimension and migration batch provenance.

---
# 441. Migration — Historical Budgets

Optional: import selected historical budget/forecast versions. Do not fabricate missing detail.

---
# 442. Migration — Actuals

Do not duplicate full historical GL as planning input. Build/read canonical actual snapshots.

---
# 443. Migration — Commitments

Opening budget-control commitments must reconcile to real open Purchase/Project/Expense source commitments where available.

---
# 444. Migration — Excel

Preview mappings, unknown members, currency, period errors and totals before commit.

---
# 445. Migration Provenance

Store SourceSystem, LegacyId, ImportBatch, AsOf and quality/notes.

---
# 446. Opening Budget Control

At go-live, current approved budget + actual YTD + open commitments must reconcile before strict block mode is enabled.

---
# 447. Shadow Mode

Recommended rollout: run Budget Control in `Warn` shadow mode before switching to `Block`, compare decisions and fix mapping gaps.

---
# 448. Go-Live Cutover

Freeze legacy planning inputs, import baseline/version, refresh Actual, reconcile commitments, publish starting version, enable controls.

---
# 449. Small Company Setup Wizard

```text
1. Choose fiscal year
2. Import/use actual history
3. Choose simple categories
4. Enter revenue/expense/payroll/capex plan
5. Review P&L
6. Approve
7. Turn on Actual-vs-Budget
```

---
# 450. Budget-Control Setup Wizard

```text
1. Select controlled spend categories
2. Choose Department/Cost Center scope
3. Choose Warn/Approve/Block
4. Choose commitment basis
5. Reconcile YTD + open commitments
6. Run shadow mode
7. Activate
```

---
# 451. Advanced FP&A Setup

```text
Models
Dimensions
Drivers
FX assumptions
Workforce
CapEx
Integrated statements
Submissions
Allocations
Scenario templates
```

---
# 452. No Setup Blockers

No Workforce model? Core financial budget still works. No CapEx model? OPEX budget works. No strict Budget Control? Planning/reporting still works.

---
# 453. SME Defaults

```text
Annual Budget            ON
Actual vs Budget         ON
Simple Forecast          ON
Scenario                 Simple
Budget Control           Warn/OFF initially
Driver Models            Optional
Workforce Planning       Simple aggregate
CapEx Planning           Simple
Integrated Balance Sheet Optional
Approvals                Simple/OFF
```

---
# 454. Mid-Market Defaults

Enable Rolling Forecast, Department submissions, commitments, budget control, driver planning, workforce, CapEx and scenario comparison as needed.

---
# 455. Enterprise Later

Advanced xP&A, complex consolidations/eliminations, Monte Carlo, predictive planning, advanced statistical forecast, dedicated OLAP engine, debt planning and advanced workforce strategy are evidence-based.

---
# 456. Regional Market Design

Middle East businesses often need multi-entity growth planning, multi-currency assumptions, CapEx/expansion plans and rapid scenario response to geopolitical/supply-chain/FX uncertainty.

---
# 457. Egypt Market Design

Support explicit EGP FX/inflation/import-cost assumptions and fast stress scenarios. Do not hardcode economic assumptions; they are user/scenario inputs.

---
# 458. GCC Market Design

Support SAR/AED and group structures, expansion/CapEx/headcount planning and long-range investment views without assuming one-country operation.

---
# 459. Arabic / English UX

Planning grid, metric labels, comments and management dashboards must support Arabic/English and RTL without breaking dense spreadsheet usability.

---
# 460. Regional Fiscal Flexibility

Do not assume calendar-year budgeting; Legal Entity fiscal calendar drives period structure.

---
# 461. Excel Reality

Treat Excel interoperability as a first-class adoption bridge while TODIR remains system of record for approved plans.

---
# 462. Market Product Position

TODIR should deliver mid-market FP&A depth without requiring separate enterprise EPM implementation for ordinary companies.

---
# 463. AI Planning Assistant

AI may summarize variances, explain drivers, propose scenario assumptions, identify anomalies and draft management commentary.

---
# 464. AI Is Advisory

AI cannot publish a Budget, approve override, reserve funds, change planning FX, or replace source Actual.

---
# 465. AI Variance Explanation

AI output must cite underlying numbers/drivers inside product and distinguish generated narrative from approved management commentary.

---
# 466. AI Forecast Suggestion

Can suggest a forecast based on history/drivers, but planner accepts/edits and published version remains human/governance controlled.

---
# 467. AI Scenario Suggestion

Can propose stress cases, e.g. sales -10%, FX +15%, but never modify Base scenario silently.

---
# 468. AI Data Access

AI uses approved Planning Tool Layer/contracts and respects dimension/actual-source permissions.

---
# 469. No AI Direct DB

Same TODIR platform rule: AI never queries/writes database directly.

---
# 470. Competitive Benchmark — Oracle EPM

Current Oracle Planning validates integrated Financials, Workforce, Capital, Projects and Strategic Modeling with driver-based planning, scenarios, long-range modeling and integrated statements.

---
# 471. Oracle Lesson — Integrated Planning

TODIR adopts linked Financial/Workforce/Capital concepts while preserving TODIR source ownership and simpler setup.

---
# 472. Oracle Lesson — Scenario

Oracle Strategic Modeling supports scenario inheritance/what-if and impact on Balance Sheet/Cash Flow/Bottom Line. TODIR adopts versioned scenarios without copying EPM administration burden.

---
# 473. Oracle Lesson — Workforce

Oracle Workforce aligns headcount and salary/benefit/tax planning with Financials. TODIR uses privacy-safe aggregate/role planning by default.

---
# 474. Oracle Lesson — Capital

Oracle Capital models CapEx requests/approvals and impact on statements. TODIR keeps planned CapEx separate from actual Fixed Asset.

---
# 475. Competitive Benchmark — Dynamics 365 Finance

Current Dynamics validates budget plan scenarios, organizational hierarchy, workflows, actual import, allocations, Excel templates and configurable budget control.

---
# 476. Dynamics Lesson — Budget Control

Adopt configurable funds-available formula, thresholds, source-document control and clear budget-vs-actual integration.

---
# 477. Dynamics Lesson — Workflow

Adopt top-down/bottom-up department planning and Excel-friendly entry without duplicating Platform Approvals.

---
# 478. Competitive Benchmark — SAP Analytics Cloud

Current SAP Planning validates public/private versions, data entry, value locks, allocations, currency conversion, data actions, input tasks, value-driver trees and scenario/predictive tools.

---
# 479. SAP Lesson — Versions

TODIR keeps private/shared/approved versions and clear publish semantics.

---
# 480. SAP Lesson — Allocations

Structured allocation rules are valuable but remain planning transformations, not journals.

---
# 481. Competitive Benchmark — Workday Adaptive Planning

Current Workday validates rolling forecasts, top-down/bottom-up budgeting, driver-based expense planning, unlimited what-if scenarios and continuous planning.

---
# 482. Workday Lesson — Speed

Scenario creation/recalibration must feel fast and collaborative, not require finance IT intervention for every change.

---
# 483. Competitive Benchmark — Anaplan

Current Anaplan Integrated Financial Planning connects Revenue/OPEX/CapEx and integrated financial statements with scenario analysis and AI-assisted finance insights.

---
# 484. Anaplan Lesson — Connected Plans

TODIR composes owner-domain plans/facts rather than duplicating them in a giant planning silo.

---
# 485. Competitive Benchmark — NetSuite Planning & Budgeting

Current NetSuite validates budgeting/forecasting, scenario modeling, workforce planning, Office integration, AI commentary and real-time ERP-connected planning.

---
# 486. NetSuite Lesson — Mid-Market

TODIR should make serious FP&A available without enterprise-only complexity.

---
# 487. Competitive Benchmark — Odoo 19

Odoo 19 validates simple analytic/financial budgets, periodic budget splitting, budget-vs-actual drilldown and familiar accounting integration.

---
# 488. Odoo Lesson — Simplicity

TODIR should preserve fast basic budgeting for SMEs while going materially deeper on forecast/scenario/control than Odoo's basic experience.

---
# 489. Competitive Product Target

> **Odoo simplicity + Dynamics budget control + Oracle/SAP connected planning + Workday/Anaplan scenario agility + NetSuite mid-market practicality.**

---
# 490. Official Reference — Dynamics Budget Planning

`https://learn.microsoft.com/en-us/dynamics365/finance/budgeting/budget-plan`

---
# 491. Official Reference — Dynamics Budget Control

`https://learn.microsoft.com/en-us/dynamics365/finance/budgeting/budget-control-overview-configuration`

---
# 492. Official Reference — Oracle Planning Modules

`https://docs.oracle.com/en/cloud/saas/planning-budgeting-cloud/epbug/launch.html`

---
# 493. Official Reference — Oracle Strategic Modeling

`https://docs.oracle.com/en/cloud/saas/planning-budgeting-cloud/planning-tutorial-introduction-to-strategic-modeling/index.html`

---
# 494. Official Reference — Oracle Workforce

`https://docs.oracle.com/en/cloud/saas/planning-budgeting-cloud/epbca/about_workforce_module_4.html`

---
# 495. Official Reference — Oracle Capital

`https://docs.oracle.com/en/cloud/saas/planning-budgeting-cloud/epbca/about_capital_module.html`

---
# 496. Official Reference — SAP Planning

`https://help.sap.com/docs/SAP_ANALYTICS_CLOUD/92a4a76cf6ae454cbf55b73df1cc2f3d/e5ee7ea08f754596a1ec1ff29cbabdcb.html`

---
# 497. Official Reference — Workday Adaptive Planning

`https://www.workday.com/en-us/products/adaptive-planning/financial-planning/budgeting-forecasting.html`

---
# 498. Official Reference — Anaplan IFP

`https://www.anaplan.com/applications/integrated-financial-planning-app/`

---
# 499. Official Reference — NetSuite Planning

`https://www.netsuite.com/portal/products/erp/financial-management/financial-planning.shtml`

---
# 500. Official Reference — Odoo Budget

`https://www.odoo.com/documentation/19.0/applications/finance/accounting/reporting/budget.html`

---
# 501. Market Reference — PwC Middle East 2026

`https://www.pwc.com/m1/en/media-centre/2026/middle-east-29-ceo-survey.html`

---
# 502. Market Design Implication

Regional growth plus geopolitical/supply-chain uncertainty supports fast scenario planning, multi-currency planning and long-range investment planning as core product requirements.

---
# 503. Do Not Copy Competitor UI

Competitor capability validates business need; TODIR retains its own simpler workflows and source-of-truth architecture.

---
# 504. Do Not Build EPM Admin Maze

No mandatory cube designer, scripting language, metadata deployment pipeline or specialist consultant workflow for normal company planning.

---
# 505. No Separate Budget Packs

Do not create separate architecture packs for Revenue Planning, OPEX, Workforce, CapEx, Budget Control, Scenario Planning or Rolling Forecast. They are slices of `28`.

---
# 506. Future Separate Capability — Consolidation

True statutory group consolidation/elimination can become separate/Accounting-adjacent capability if required.

---
# 507. Future Separate Capability — Advanced Treasury

Debt, hedging and capital-structure treasury remain outside basic `28`.

---
# 508. Future Separate Capability — Sales Planning

If territory/quota/sales capacity becomes deep enough, future Sales Planning may emerge; core revenue plan remains `28`.

---
# 509. Future Separate Capability — Strategic Workforce

Deep skills/succession/workforce optimization belongs HR/advanced workforce, not core FP&A.

---
# 510. Release Philosophy

`28` is trustworthy only when every approved number has version, period, scope, provenance, driver and governance.

---
# 511. No Mystery Cell

If a user cannot tell whether a number is entered/calculated/actual/allocated/overridden, product design is incomplete.

---
# 512. No Mystery Available Budget

Available funds must drill to Current Budget, Actual, Commitments, Reservations and proposed amount.

---
# 513. No Mystery Forecast

Forecast must state Actual cutoff, future method and assumptions.

---
# 514. No Mystery FX

Every converted planning amount can explain rate/scenario/period/rate type.

---
# 515. No Mystery Rollup

Group total shows included entities and translation basis.

---
# 516. No Silent Baseline Rewrite

Approved Budget/Forecast version is immutable.

---
# 517. No Planning Journal

Planning values never create journal entries by themselves.

---
# 518. No Budget Control Through Stale Batch

Strict block decisions cannot depend on stale nightly Actual/Commitment snapshots.

---
# 519. No Source Domain Fork

If upstream source fact is missing, extend the owning contract; do not create Planning-owned duplicate truth.

---
# 520. No Price Of Simplicity = Wrong Finance

SME UX may hide dimensions/workflows, but integrity/versioning/provenance remain mandatory.

---
# 521. Agent Implementation Philosophy

Build Planning as ordered business slices. Do not start with a generic cube designer or AI forecast before Budget/Actual/Version/Control invariants work.

---
# 522. Agent Role Directive

Implementation Agent acts as Senior Product Architect + Senior Software/Systems Architect + Senior Developer/Engineer. Business workflow and source ownership come first.

---
# 523. Agent Precedence

If conflict:
```text
28 FP&A pack
→ owning source-domain pack
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ approved ADR
```

---
# 524. Agent Must Read

At minimum:
```text
01 02 03 06 09 10 13 14 15 16 17 18 21 22 23 25 26 28
all AGENTS.md
approved ADRs
actual source contracts
```

---
# 525. Repository Baseline Report

Before code report:
```text
Accounting actual contracts?
Planning dimensions/mappings?
Project budget contracts?
Purchasing commitment lifecycle?
Platform Approvals?
RLS/Transaction Coordinator?
Jobs/Outbox?
Grid framework?
```

---
# 526. Agent Must Not Duplicate Ledger

No `PlanningJournalEntry`, no editable Actual financial truth.

---
# 527. Agent Must Not Duplicate Projects

No Planning-owned Project Budget/ETC/EAC copy as source truth.

---
# 528. Agent Must Not Duplicate Banking

No TreasuryAccount or short-term bank cash forecast engine in `28`.

---
# 529. Agent Must Not Duplicate HR/Payroll

No fake Employees/Payslips for workforce planning.

---
# 530. Agent Must Not Duplicate Fixed Assets

No planned item becomes Asset before actual acquisition/capitalization.

---
# 531. Agent Task Template

Every phase uses:
```text
TASK
BUSINESS GOAL
REAL-WORLD WORKFLOW
SOURCE OF TRUTH
STATE / INVARIANTS
FILES TO READ
FILES ALLOWED TO MODIFY
DO NOT MODIFY
CALCULATION REQUIREMENTS
DATABASE REQUIREMENTS
INTEGRATION REQUIREMENTS
SECURITY REQUIREMENTS
PERFORMANCE REQUIREMENTS
UX REQUIREMENTS
TESTS REQUIRED
ACCEPTANCE CRITERIA
EXIT GATE
REPORT BACK WITH
```

---
# 532. Implementation Sequence

Recommended:
```text
FP00 Repository / Dependency Verification
FP01 Module Boundary + Schema + RLS
FP02 Fiscal Calendar + Planning Cycle
FP03 Planning Model + Controlled Dimensions
FP04 Metrics + Mapping Foundation
FP05 Sparse Coordinates + Input Facts
FP06 Versioning / Scenario Core
FP07 Grid Query + Batch Write
FP08 Actual Snapshot / Ledger Integration
FP09 Budget Baseline
FP10 Budget Revisions + Transfers
FP11 Submission / Workflow
FP12 Assumptions / Drivers
FP13 Formula Engine / DAG
FP14 Calculation Runs + Trace
FP15 Spreading
FP16 Allocations
FP17 Revenue Planning
FP18 OPEX Planning
FP19 Workforce Planning
FP20 CapEx Planning
FP21 Planning FX
FP22 Integrated P&L
FP23 Working Capital
FP24 Projected Balance Sheet
FP25 Projected Cash Flow
FP26 Rolling Forecast
FP27 Scenario / What-If
FP28 Variance Analysis
FP29 Forecast Accuracy
FP30 Budget Control Policy
FP31 Budget Availability Projection
FP32 Synchronous Check / Reserve
FP33 Purchasing Precommitment/Commitment
FP34 Expenses / Other Commitment Integrations
FP35 Commitment → Actual Reconciliation
FP36 Overrides / Thresholds
FP37 Department Planner UX
FP38 CFO / Owner Views
FP39 Excel Import / Export / Paste
FP40 Multi-Entity Rollup
FP41 Security / SoD
FP42 Audit / History
FP43 Migration / Opening Budget
FP44 Performance / Scale
FP45 Observability
FP46 Runbooks / Recovery
FP47 Small Company Acceptance
FP48 Department Manager Acceptance
FP49 FP&A Acceptance
FP50 CFO Acceptance
FP51 Purchasing Budget-Control Acceptance
FP52 Cross-Module Reconciliation
FP53 E2E / Release Gate
```

---
# 533. FP00 — Repository / Dependency Verification

**Goal**

Map existing source contracts and conflicts; no assumptions about repository truth.

**Exit Gate**

Dependency matrix Exists/Partial/Missing/Conflict completed; no duplicate source ownership planned.

---
# 534. FP01 — Module Boundary + Schema + RLS

**Goal**

Create Todir.Modules.Planning.*, Todir.Planning.Contracts, business_planning, PlanningDbContext and architecture tests.

**Exit Gate**

Module boots isolated; no direct cross-module DbContext access.

---
# 535. FP02 — Fiscal Calendar + Planning Cycle

**Goal**

Model cycles/horizons/actual cutoff against canonical fiscal periods.

**Exit Gate**

Annual/forecast/long-range cycles work without date ambiguity.

---
# 536. FP03 — Planning Model + Controlled Dimensions

**Goal**

Create typed model definitions and activated dimensions.

**Exit Gate**

Simple model works with Account×Month; advanced model can add approved dimensions.

---
# 537. FP04 — Metrics + Mapping Foundation

**Goal**

Map actual accounts/roles and operational metrics into planning metrics.

**Exit Gate**

Every imported Actual has versioned mapping provenance.

---
# 538. FP05 — Sparse Coordinates + Input Facts

**Goal**

Build sparse input storage and coordinate dedupe.

**Exit Gate**

Null/zero distinction and bulk input correctness pass.

---
# 539. FP06 — Versioning / Scenario Core

**Goal**

Create Draft/Published versions, clone and private/shared scenarios.

**Exit Gate**

Published immutable; scenario cannot mutate baseline.

---
# 540. FP07 — Grid Query + Batch Write

**Goal**

Deliver spreadsheet-like query/write endpoints and virtualization contract.

**Exit Gate**

Thousands of cells can paste/write without per-cell requests.

---
# 541. FP08 — Actual Snapshot / Ledger Integration

**Goal**

Load read-only aggregated Actual with watermark/reconciliation.

**Exit Gate**

Snapshot reconciles to configured Accounting totals.

---
# 542. FP09 — Budget Baseline

**Goal**

Create Original Budget and publish semantics.

**Exit Gate**

Original Budget remains immutable after approval.

---
# 543. FP10 — Budget Revisions + Transfers

**Goal**

Create explicit revisions and conserving transfers.

**Exit Gate**

Current Budget derives from baseline + approved deltas.

---
# 544. FP11 — Submission / Workflow

**Goal**

Department slices, submit/return/approve using Platform Approvals.

**Exit Gate**

Submitted slice locks correctly and preserves history.

---
# 545. FP12 — Assumptions / Drivers

**Goal**

Build versioned assumptions and typed driver inputs.

**Exit Gate**

Driver provenance visible and scenario-specific.

---
# 546. FP13 — Formula Engine / DAG

**Goal**

Constrained DSL, typed validation and cycle detection.

**Exit Gate**

Deterministic golden vectors pass; cycles rejected.

---
# 547. FP14 — Calculation Runs + Trace

**Goal**

Persist runs, inputs/watermarks and why-this-number traces.

**Exit Gate**

Calculated output can explain lineage.

---
# 548. FP15 — Spreading

**Goal**

Even/seasonal/prior/custom spread with exact totals.

**Exit Gate**

Residual rounding preserves source total.

---
# 549. FP16 — Allocations

**Goal**

Driver-based allocations with trace and versioning.

**Exit Gate**

Allocated totals conserve source amount and drill back.

---
# 550. FP17 — Revenue Planning

**Goal**

Manual and driver-based volume×price planning.

**Exit Gate**

No Sales transaction duplication; Actual revenue mapping works.

---
# 551. FP18 — OPEX Planning

**Goal**

Department/category OPEX input methods and assumptions.

**Exit Gate**

Simple and driver OPEX flows work.

---
# 552. FP19 — Workforce Planning

**Goal**

Aggregate/role-based headcount and cost planning.

**Exit Gate**

No Employee/Payslip creation and privacy tests pass.

---
# 553. FP20 — CapEx Planning

**Goal**

Investment requests, timing and planned depreciation.

**Exit Gate**

No Fixed Asset/PO creation from plan alone.

---
# 554. FP21 — Planning FX

**Goal**

Scenario/period planning rates and conversion trace.

**Exit Gate**

Planning FX remains separate from actual transaction FX.

---
# 555. FP22 — Integrated P&L

**Goal**

Build projected income statement.

**Exit Gate**

Every line traces to plan inputs/mapping.

---
# 556. FP23 — Working Capital

**Goal**

DSO/DPO/Inventory-day driver models.

**Exit Gate**

Driver changes affect AR/AP/Inventory forecasts deterministically.

---
# 557. FP24 — Projected Balance Sheet

**Goal**

Opening Actual + planned movements.

**Exit Gate**

Balance check passes or clearly reports model gap.

---
# 558. FP25 — Projected Cash Flow

**Goal**

Derive cash flow/ending cash from integrated model.

**Exit Gate**

Ending projected cash reconciles to Balance Sheet.

---
# 559. FP26 — Rolling Forecast

**Goal**

Actual cutoff + future forecast + horizon roll.

**Exit Gate**

Closed period converts to Actual without rewriting old forecast snapshot.

---
# 560. FP27 — Scenario / What-If

**Goal**

Fast clone/edit/compare/adopt flow.

**Exit Gate**

What-if cannot alter approved versions.

---
# 561. FP28 — Variance Analysis

**Goal**

Budget/Forecast/Actual variance and driver decomposition.

**Exit Gate**

Sign/materiality definitions explicit.

---
# 562. FP29 — Forecast Accuracy

**Goal**

Compare historical forecast snapshots to later Actual.

**Exit Gate**

Accuracy metrics reproducible.

---
# 563. FP30 — Budget Control Policy

**Goal**

Control scope, basis, modes and thresholds.

**Exit Gate**

Policy resolves deterministically for source transaction.

---
# 564. FP31 — Budget Availability Projection

**Goal**

Near-real-time CurrentBudget/Actual/Commitment/Available.

**Exit Gate**

Projection rebuild/reconciliation passes.

---
# 565. FP32 — Synchronous Check / Reserve

**Goal**

Atomic preview vs reservation semantics.

**Exit Gate**

Concurrent overspend prevented.

---
# 566. FP33 — Purchasing Commitment Integration

**Goal**

PR precommitment and PO commitment chain.

**Exit Gate**

PR→PO does not double-count.

---
# 567. FP34 — Expenses / Other Commitments

**Goal**

Integrate approved source commitments without duplicate ownership.

**Exit Gate**

Source corrections/release reconcile.

---
# 568. FP35 — Commitment → Actual Reconciliation

**Goal**

Invoice/posted Actual consumes/replaces commitment correctly.

**Exit Gate**

Partial invoice and cancellation cases pass.

---
# 569. FP36 — Overrides / Thresholds

**Goal**

Typed override and utilization thresholds.

**Exit Gate**

Override does not mutate Budget baseline.

---
# 570. FP37 — Department Planner UX

**Goal**

Fast scoped planning workspace.

**Exit Gate**

Manager plans/submits without finance-admin clutter.

---
# 571. FP38 — CFO / Owner Views

**Goal**

Management KPIs, risks, cash, margin, headcount and CapEx.

**Exit Gate**

Every KPI drills to source/basis.

---
# 572. FP39 — Excel Interop

**Goal**

Import/export/paste with validation preview.

**Exit Gate**

Excel remains interface, not competing source of truth.

---
# 573. FP40 — Multi-Entity Rollup

**Goal**

Authorized management rollup with planning FX.

**Exit Gate**

Clearly not statutory consolidation.

---
# 574. FP41 — Security / SoD

**Goal**

Dimension/scenario/sensitive workforce/override permissions.

**Exit Gate**

Cross-tenant/entity/slice red-team passes.

---
# 575. FP42 — Audit / History

**Goal**

Critical edit/publish/control histories.

**Exit Gate**

Published history fully explainable.

---
# 576. FP43 — Migration / Opening Budget

**Goal**

Import baseline, YTD Actual and open commitments.

**Exit Gate**

Opening Budget Control reconciles before strict mode.

---
# 577. FP44 — Performance / Scale

**Goal**

Grid, calculation, control, rollup load tests.

**Exit Gate**

Targets pass or ADR documents limitation.

---
# 578. FP45 — Observability

**Goal**

Metrics/traces/jobs/control health.

**Exit Gate**

Operators can diagnose stale/recalc/control failures.

---
# 579. FP46 — Runbooks / Recovery

**Goal**

Semantic recovery for failed jobs/control mismatches/version mistakes.

**Exit Gate**

No routine DB surgery required.

---
# 580. FP47 — Small Company Acceptance

**Goal**

Simple annual budget and actual-vs-budget.

**Exit Gate**

Company can leave Excel for normal budgeting without EPM complexity.

---
# 581. FP48 — Department Manager Acceptance

**Goal**

Plan own budget, comments, submit and understand available funds.

**Exit Gate**

No unauthorized finance data exposure.

---
# 582. FP49 — FP&A Acceptance

**Goal**

Drivers, forecast, scenarios, variance and statements.

**Exit Gate**

Planner can explain each management number.

---
# 583. FP50 — CFO Acceptance

**Goal**

Answer company outlook, cash, margin, spend, hiring, CapEx and downside cases.

**Exit Gate**

No manual consolidation spreadsheet for normal management view.

---
# 584. FP51 — Purchasing Budget-Control Acceptance

**Goal**

Check/reserve/override/commitment lifecycle under concurrency.

**Exit Gate**

No double commitment or hidden bypass.

---
# 585. FP52 — Cross-Module Reconciliation

**Goal**

Reconcile Actual, commitments, Projects, Payroll, Assets and Banking boundaries.

**Exit Gate**

Zero unexplained source-of-truth differences.

---
# 586. FP53 — E2E / Release Gate

**Goal**

Run end-to-end planning-to-control-to-actual flows.

**Exit Gate**

Release only when history, controls, calculations and reconciliation are trustworthy.

---
# 587. Business Scenario — Annual Budget — Small Company

**Flow**

Prior-year Actual → simple revenue/OPEX/payroll/capex inputs → P&L preview → approve.

**Acceptance**

Approved baseline immutable; monthly Actual comparison works.

---
# 588. Business Scenario — Department Bottom-Up

**Flow**

Finance opens cycle → departments enter needs → submit → Finance review → CFO approve.

**Acceptance**

Submission locking and responsibility correct.

---
# 589. Business Scenario — Top-Down vs Bottom-Up

**Flow**

Executive envelope 30M vs requests 34M.

**Acceptance**

4M gap visible; neither side overwritten.

---
# 590. Business Scenario — Budget Revision

**Flow**

Approved 100M → approved +5M revision.

**Acceptance**

Original stays 100M; Current becomes 105M.

---
# 591. Business Scenario — Budget Transfer

**Flow**

Marketing -1M → IT +1M.

**Acceptance**

Total conserved; both buckets auditable.

---
# 592. Business Scenario — Rolling Forecast

**Flow**

Actual Jan-Apr + May-Apr future forecast.

**Acceptance**

Next month rolls correctly; prior forecast preserved.

---
# 593. Business Scenario — FX Stress

**Flow**

USD/EGP 55 → 70 for Stress scenario.

**Acceptance**

Imported-cost/margin/cash impact recalculates; Base unchanged.

---
# 594. Business Scenario — Revenue Driver

**Flow**

Units 10k × Price 500.

**Acceptance**

Revenue 5M trace shows drivers.

---
# 595. Business Scenario — Workforce Hire

**Flow**

5 Sales Reps start March at role-average cost.

**Acceptance**

FTE/cost proration correct; no Employees created.

---
# 596. Business Scenario — CapEx Machine

**Flow**

Plan 5M machine in June, in service July.

**Acceptance**

CapEx and planned depreciation shown; no Asset/PO created.

---
# 597. Business Scenario — Budget Check Allow

**Flow**

Available 500k; proposed PO 100k.

**Acceptance**

Allow with 400k after proposed.

---
# 598. Business Scenario — Budget Check Block

**Flow**

Available 100k; proposed PO 300k under Block.

**Acceptance**

Source approval fails without reservation.

---
# 599. Business Scenario — Concurrent PO

**Flow**

Two 80k POs race for 100k availability.

**Acceptance**

At most one strict reservation succeeds.

---
# 600. Business Scenario — PR To PO

**Flow**

PR 50k precommitted then converted to PO 50k.

**Acceptance**

Total budget consumption remains 50k.

---
# 601. Business Scenario — Partial Invoice

**Flow**

PO 100k; invoice 40k.

**Acceptance**

Actual 40k + open commitment 60k.

---
# 602. Business Scenario — PO Cancellation

**Flow**

Open PO commitment cancelled.

**Acceptance**

Remaining commitment released once.

---
# 603. Business Scenario — Forecast Over Budget

**Flow**

Current Budget 10M; Forecast 12M.

**Acceptance**

Warning shows expected 2M overrun; Budget not increased.

---
# 604. Business Scenario — Project Envelope

**Flow**

Corporate CapEx envelope 50M references Project budgets.

**Acceptance**

Project detailed EAC remains Project-owned.

---
# 605. Business Scenario — Integrated Statements

**Flow**

Revenue/OPEX/working-capital/capex drivers produce three statements.

**Acceptance**

Balance Sheet balances and cash reconciles.

---
# 606. Business Scenario — Multi-Entity Rollup

**Flow**

EGP/SAR/AED entity plans converted to reporting currency.

**Acceptance**

Rate basis visible; no statutory-consolidation claim.

---
# 607. Test — Actual Read-Only

Planner cannot edit Actual cell.

---
# 608. Test — Published Budget Immutable

Published baseline rejects ordinary edit.

---
# 609. Test — Revision Delta

Revision changes Current not Original.

---
# 610. Test — Transfer Conservation

Transfer out/in conserve amount.

---
# 611. Test — Scenario Isolation

What-if never changes Budget/Forecast.

---
# 612. Test — Clone Version

Clone preserves source reference and makes independent draft.

---
# 613. Test — Null vs Zero

Explicit zero differs from missing input.

---
# 614. Test — Formula Cycle

Cycle is rejected before calculation.

---
# 615. Test — Formula Determinism

Same inputs/version returns same result.

---
# 616. Test — Formula Trace

Calculated value explains dependencies.

---
# 617. Test — Override Audit

Override records original/calculated/override/reason.

---
# 618. Test — Spread Total

Period spread totals exact source amount.

---
# 619. Test — Allocation Conservation

Allocated destinations equal source.

---
# 620. Test — Allocation Trace

Every destination traces to source/weight.

---
# 621. Test — Actual Snapshot Reconcile

Mapped Actual totals reconcile to Accounting.

---
# 622. Test — Actual Mapping Version

Mapping change is versioned.

---
# 623. Test — Planning FX

Conversion uses scenario/period rate, not actual transaction rate.

---
# 624. Test — FX Trace

Converted value exposes source/rate/result.

---
# 625. Test — Revenue Driver

Volume×price golden vector correct.

---
# 626. Test — Workforce Proration

Midyear hire cost prorates correctly.

---
# 627. Test — Workforce Privacy

Unauthorized planner cannot see individual compensation.

---
# 628. Test — CapEx No Asset

Approved CapEx does not create Fixed Asset.

---
# 629. Test — CapEx No PO

Approved CapEx does not create PO.

---
# 630. Test — Projected Balance

Integrated model balance check works.

---
# 631. Test — Projected Cash

Cash Flow ending cash reconciles.

---
# 632. Test — Budget Preview

Preview does not reserve.

---
# 633. Test — Budget Reserve

Reserve changes availability exactly once.

---
# 634. Test — Reserve Retry

Idempotent retry no duplicate.

---
# 635. Test — Concurrent Reserve

Overspend race prevented.

---
# 636. Test — Strict Service Failure

Block mode fails closed when control unavailable.

---
# 637. Test — PR Precommitment

Approved PR consumes configured precommitment.

---
# 638. Test — PR To PO

Precommitment replaced by commitment.

---
# 639. Test — PO Partial Actual

Commitment decreases by actual recognized amount.

---
# 640. Test — PO Increase

Additional amount rechecks budget.

---
# 641. Test — PO Decrease

Excess commitment released.

---
# 642. Test — PO Cancel

Remaining commitment released.

---
# 643. Test — Budget Override

Override permits source only; budget unchanged.

---
# 644. Test — Threshold Warn

Utilization threshold triggers expected warning.

---
# 645. Test — Cross Currency Control

Budget conversion uses configured control FX.

---
# 646. Test — Multi-Bucket Atomic

All buckets reserve or none.

---
# 647. Test — Control Rebuild

Projection rebuild equals incremental state.

---
# 648. Test — Submission Lock

Submitted slice cannot be edited by planner.

---
# 649. Test — Return For Revision

Returned slice becomes editable.

---
# 650. Test — Approval SoD

Configured maker/checker separation enforced.

---
# 651. Test — Scenario Security

Private scenario hidden from unauthorized users.

---
# 652. Test — Actual Drillback Security

No unauthorized source-detail exposure.

---
# 653. Test — Excel Paste

Bulk paste validates cell coordinates/UOM/currency.

---
# 654. Test — Paste Concurrency

Stale version write rejected.

---
# 655. Test — Grid Scale

Large virtual grid uses bounded query count.

---
# 656. Test — Calculation Impact

Driver change recalculates dependent slice only.

---
# 657. Test — Forecast Roll

Closed forecast period becomes Actual without rewriting prior snapshot.

---
# 658. Test — Forecast Accuracy

Historical forecast vs later Actual reproducible.

---
# 659. Test — Project Source

Project EAC shown from Projects, not copied editable.

---
# 660. Test — Treasury Boundary

Financial forecast does not create Treasury account forecast lines.

---
# 661. Test — Manufacturing Boundary

FP&A does not create MRP orders.

---
# 662. Test — Payroll Boundary

Workforce plan does not create Payslip.

---
# 663. Test — Accounting Boundary

Plan does not create Journal Entry.

---
# 664. Test — RLS Cross Tenant

Tenant A cannot access B plan.

---
# 665. Test — Cross Entity Scope

Planner sees only authorized entities.

---
# 666. Test — Export Security

Export respects plan/source permissions.

---
# 667. Test — Audit Publish

Publication event and exact version recorded.

---
# 668. Test — Archived History

Old published plan remains readable.

---
# 669. Test — No Silent Restatement

Actual restatement status visible in comparison.

---
# 670. Test — Group Rollup

Authorized entities only; FX basis explicit.

---
# 671. Test — Zero Budget

Zero is treated as explicit controlled amount.

---
# 672. Test — Negative Available

Overrun shown explicitly, not clamped to zero.

---
# 673. Test — Commitment Correction

Reversal restores correct availability once.

---
# 674. Test — Period Carryforward

Carryforward obeys explicit policy.

---
# 675. Test — Monthly vs Annual Control

Period control does not borrow annual funds unless configured.

---
# 676. Test — Forecast Commentary

Comment tied to exact coordinate/version.

---
# 677. Test — Variance Sign

Revenue/expense favorable sign rules correct.

---
# 678. Test — Materiality

Comment requirement triggers only over threshold.

---
# 679. Test — Scenario Adoption

Adopt creates draft version; no direct publish.

---
# 680. Test — Planning FX Scenario

Changing FX in Stress leaves Base unchanged.

---
# 681. Test — Private What-If

Personal scenario changes are isolated.

---
# 682. Test — Calculated Cell Edit

Direct edit blocked unless override allowed.

---
# 683. Test — Stale Projection

UI/API exposes stale status.

---
# 684. Test — Actual Refresh Failure

Current published snapshot remains available.

---
# 685. Test — Recalc Failure

Published previous calculation remains; draft marks error.

---
# 686. Test — Migration Baseline

Imported approved budget reconciles to source totals.

---
# 687. Test — Opening Commitment

Imported/open commitments reconcile to Purchasing.

---
# 688. Test — Shadow Mode

Warn decisions recorded without blocking.

---
# 689. Test — Switch To Block

Strict mode enabled only after reconciliation gate.

---
# 690. Property Test — Budget Conservation

CurrentBudget = Original + Revisions + TransferIn - TransferOut.

---
# 691. Property Test — Available Funds

Available equals configured components exactly; no hidden term.

---
# 692. Property Test — Commitment Chain

One economic amount cannot be simultaneously double-counted as PR+PO+Actual.

---
# 693. Property Test — Allocation Conservation

Sum allocated equals source after deterministic rounding.

---
# 694. Property Test — Spread Conservation

Sum periods equals source total.

---
# 695. Property Test — Three-Statement Cash

Projected ending cash reconciles between Balance Sheet and Cash Flow.

---
# 696. Property Test — Version Immutability

Published version hash/data cannot change through normal commands.

---
# 697. Property Test — Scenario Isolation

Scenario changes cannot mutate source version.

---
# 698. Property Test — Actual Provenance

Every Actual fact has source/mapping/watermark.

---
# 699. Property Test — Planning FX

Every converted planning amount identifies rate basis.

---
# 700. Architecture Test — No Ledger Writes

Planning assembly cannot write Ledger persistence.

---
# 701. Architecture Test — No Project Budget Copy

Planning cannot own authoritative Project Budget table.

---
# 702. Architecture Test — No Treasury Account

Planning cannot own TreasuryAccount/Bank Statement.

---
# 703. Architecture Test — No Employee Copy

Workforce plan cannot own Employee.

---
# 704. Architecture Test — No Asset Copy

CapEx plan cannot own FixedAsset.

---
# 705. Architecture Test — No PO Copy

Planning cannot own PurchaseOrder.

---
# 706. Architecture Test — No MRP Copy

Planning cannot own ProductionOrder/MRP.

---
# 707. Architecture Test — No Arbitrary Script

Formula engine rejects tenant executable code.

---
# 708. Architecture Test — RLS Mandatory

Planning tenant tables require FORCE RLS.

---
# 709. Architecture Test — Budget Sync Contract

Strict Budget Control uses approved synchronous contract, not async-only event.

---
# 710. Red-Team — Edit Actual

Reject.

---
# 711. Red-Team — Overwrite Original Budget

Reject.

---
# 712. Red-Team — Scenario Directly Changes Budget

Reject.

---
# 713. Red-Team — Budget Revision As Silent Edit

Reject.

---
# 714. Red-Team — Transfer As Two Manual Edits

Reject.

---
# 715. Red-Team — Forecast Posts Journal

Reject.

---
# 716. Red-Team — Planned Hire Creates Employee

Reject.

---
# 717. Red-Team — CapEx Plan Creates Asset

Reject.

---
# 718. Red-Team — Budget Pass Means PO Approved

Reject.

---
# 719. Red-Team — Nightly Snapshot Used For Strict Check

Reject.

---
# 720. Red-Team — PR+PO+Invoice Double Count

Reject.

---
# 721. Red-Team — Two Concurrent POs Overspend

Reject.

---
# 722. Red-Team — Planning FX Reused As Actual FX

Reject.

---
# 723. Red-Team — Group Rollup Called Consolidated Statements

Reject.

---
# 724. Red-Team — AI Publishes Forecast

Reject.

---
# 725. Red-Team — Formula Executes SQL

Reject.

---
# 726. Red-Team — Excel Becomes Uncontrolled Master

Reject.

---
# 727. Red-Team — Missing Input Treated As Zero Silently

Reject.

---
# 728. Red-Team — Last Write Wins On Same Plan Slice

Reject.

---
# 729. Red-Team — CFO Dashboard Number Without Drilldown

Reject.

---
# 730. Red-Team — Project EAC Reimplemented

Reject.

---
# 731. Red-Team — Treasury Forecast Reimplemented

Reject.

---
# 732. Red-Team — Payroll Salary Leaked Through Workforce Plan

Reject.

---
# 733. Red-Team — Budget Override Increases Baseline

Reject.

---
# 734. Red-Team — Unexplained Balance Sheet Plug

Reject.

---
# 735. Reports — Budget

```text
Original vs Current Budget
Budget by Department
Budget by Category
Budget Revisions
Budget Transfers
Budget Utilization
Available Budget
Budget Overrides
```

---
# 736. Reports — Forecast

```text
Full-Year Forecast
Rolling Forecast
Forecast vs Budget
Forecast vs Actual
Forecast Accuracy
Forecast Revision History
```

---
# 737. Reports — Scenario

```text
Scenario Comparison
Assumption Comparison
Stress Impact
Target Gap
```

---
# 738. Reports — Workforce

```text
Planned vs Actual Headcount
Planned Hires
Planned Workforce Cost
Headcount by Department
Workforce Cost Variance
```

---
# 739. Reports — CapEx

```text
CapEx Pipeline
Approved CapEx Budget
Planned vs Actual CapEx
CapEx Timing Variance
Investment by Category
```

---
# 740. Reports — Management Statements

```text
Projected P&L
Projected Balance Sheet
Projected Cash Flow
Actual vs Budget P&L
Actual vs Forecast P&L
```

---
# 741. Reports — Budget Control

```text
Near-Limit Budgets
Blocked Transactions
Override Register
Open Precommitments
Open Commitments
Stale Commitments
Budget Control Reconciliation
```

---
# 742. Drilldown Rule

Every report number drills to planning coordinate, source facts, assumptions or control reservations subject to permissions.

---
# 743. Executive Summary

CFO/Owner should answer:
```text
Are we on revenue plan?
Are margins holding?
Where are we overspending?
What do we now expect for year-end?
How much cash may we need?
What hiring/capex is planned?
Which downside scenario matters?
```

---
# 744. Risk View

Highlight material downside, forecast misses, overspend, liquidity stress and target gaps; not a generic red-card dashboard.

---
# 745. Opportunity View

Can highlight upside scenario, underutilized budget or stronger-than-plan performance without confusing it with approved plan.

---
# 746. Board Pack Seam

Future/optional board-pack generation can compose approved views/comments. Do not create independent financial truth.

---
# 747. Management Commentary Pack

Comments may be selected into monthly/quarterly management narrative with source links.

---
# 748. No PDF-Only Planning

Reports can export, but interactive drilldown remains core.

---
# 749. Permissions — Initial Vocabulary

```text
planning.cycles.read
planning.cycles.manage
planning.models.read
planning.models.manage
planning.versions.read
planning.versions.edit
planning.versions.submit
planning.versions.approve
planning.versions.publish
planning.scenarios.create
planning.scenarios.share
planning.assumptions.manage
planning.budget.read
planning.budget.manage
planning.budget.revision
planning.budget.transfer
planning.budget.transfer.approve
planning.budget_control.read
planning.budget_control.override
planning.budget_control.override.approve
planning.workforce.read
planning.workforce.sensitive.read
planning.capex.read
planning.capex.manage
planning.variance.read
planning.actuals.refresh
planning.export
planning.audit.read
```

---
# 750. Permission Scope

Combine permission with LegalEntity, Model, Department/CostCenter, Branch, Scenario/Version and sensitive class.

---
# 751. Budget Owner Permissions

Can edit assigned budget slice, see its Actual/Commitment/Available, submit and comment; cannot change model formulas or other departments.

---
# 752. Reviewer Permissions

Can review assigned slices and return/approve per workflow.

---
# 753. FP&A Permissions

Can build/operate planning cycles/models within authorization, but sensitive source details remain independently protected.

---
# 754. Executive Permissions

Broad management rollup does not automatically grant transaction-level payroll/supplier detail.

---
# 755. Security Redaction

Workforce planning can expose total payroll cost while hiding individual salary.

---
# 756. Scenario Confidentiality

Confidential restructuring/acquisition scenarios can be named-user only.

---
# 757. Audit Export

Audit export is restricted and itself audited.

---
# 758. Runbook — Wrong Budget Baseline

If unpublished: correct draft. If published: create explicit correction/revision; never edit published baseline.

---
# 759. Runbook — Wrong Transfer

Reverse with compensating approved transfer; preserve original.

---
# 760. Runbook — Wrong Revision

Create corrective revision/superseding action according to state; preserve history.

---
# 761. Runbook — Duplicate Reservation

Use idempotency/source key investigation and semantic release/correction; do not delete rows manually.

---
# 762. Runbook — Missing Commitment

Reconcile source document, rebuild/import missing budget consumption fact and verify bucket balance.

---
# 763. Runbook — Stale Commitment

Owner module closes/corrects source; Planning refreshes/reconciles. Planning does not cancel PO itself.

---
# 764. Runbook — Actual Snapshot Mismatch

Stop publish, compare mapping/watermark to Accounting source, fix mapping, rebuild snapshot and reconcile.

---
# 765. Runbook — Broken Formula

Draft model version correction + golden tests + recalculation. Published version remains reproducible.

---
# 766. Runbook — Formula Cycle

Block activation; identify dependency loop; redesign formula. Never rely on iterative accidental convergence Day 1.

---
# 767. Runbook — Allocation Error

Reverse/re-run draft allocation or create corrected version; published plan not silently rewritten.

---
# 768. Runbook — FX Assumption Error

Correct scenario/version assumption and recalc; do not alter Actual FinBase FX.

---
# 769. Runbook — Forecast Published Wrong

Create corrected forecast version and supersede/label prior one; preserve both.

---
# 770. Runbook — Scenario Leaked

Revoke access, audit exports/access, investigate authorization issue; scenario facts remain intact for security review.

---
# 771. Runbook — Budget Control Service Down

Strict Block: fail closed and alert. Warn mode: follow configured degraded policy with audit.

---
# 772. Runbook — Recalc Job Failed

Keep last completed result; draft/version shows failed/stale; retry idempotently after root-cause fix.

---
# 773. Runbook — Actual Refresh Failed

Do not replace current valid snapshot; surface stale status and retry.

---
# 774. Runbook — Migration Mismatch

Do not activate strict control until approved budget/YTD Actual/open commitments reconcile.

---
# 775. Runbook — Projection Corruption

Rebuild derived projection from canonical facts and compare checksums/totals.

---
# 776. Runbook — DR Restore

Validate published versions, model versions, budget reservations, source watermarks and control balances after restore.

---
# 777. Forbidden Runbook

Never normal fix:
```text
UPDATE planning_fact SET value=...
UPDATE budget_balance SET available=...
DELETE budget_reservation...
UPDATE plan_version SET state='Published'...
```
Use semantic commands/migrations.

---
# 778. Small Company Acceptance

Owner/finance user creates budget, sees monthly P&L, compares Actual, updates simple Forecast and understands variance without spreadsheets for normal operation.

---
# 779. Department Manager Acceptance

Manager sees only own slice, past Actual, commitments, available budget, enters forecast/commentary and submits quickly.

---
# 780. FP&A Analyst Acceptance

Can build Base/Upside/Downside, adjust assumptions, recalc, compare, trace drivers and publish approved Forecast.

---
# 781. CFO Acceptance

Can answer where company will finish on Revenue/Profit/Cash, where spend is at risk, hiring/capex outlook and effect of major scenario assumptions.

---
# 782. Purchasing Acceptance

Buyer sees budget result inside purchase workflow; no need to open FP&A; strict reservation prevents overspend.

---
# 783. Project Acceptance

Corporate finance sees Project portfolio impacts without taking ownership of Project Budget/EAC.

---
# 784. HR/Payroll Acceptance

Workforce plan uses safe aggregates/roles; actual payroll comparison works without exposing unnecessary employee salary.

---
# 785. Accounting Acceptance

Actual mappings/reconciliation trustworthy; Planning never writes GL.

---
# 786. Treasury Acceptance

CFO can compare long-range financial cash plan with Treasury short-term liquidity while both remain distinct.

---
# 787. Performance Acceptance

Planner grid, strict budget check and dashboards meet measured targets on representative data.

---
# 788. Security Acceptance

Cross-tenant/entity/dimension/scenario access and sensitive-source drillback red-team passes.

---
# 789. Build Now — MUST

```text
Planning Cycles
Typed Planning Models
Controlled Dimensions
Metrics / Actual Mapping
Sparse Planning Facts
Versioning
Spreadsheet Grid
Actual Snapshots
Original Budget
Budget Revisions
Budget Transfers
Department Submission
Assumptions / Drivers
Constrained Formula Engine
Calculation Trace
Spreading
Allocations foundation
Revenue Plan
OPEX Plan
Workforce Plan
CapEx Plan
Planning FX
Rolling Forecast
Scenarios / What-If
Variance Analysis
Forecast Accuracy foundation
Budget Control Policies
Budget Availability
Transactional Check/Reserve
Purchasing Commitment Integration
Commitment→Actual Reconciliation
Overrides / Thresholds
CFO / Department Views
Excel Import/Export
Multi-Entity Management Rollup
Permissions/RLS/Audit
Migration
Performance
Runbooks
Acceptance Gates
```

---
# 790. Build Later — Evidence Based

```text
Monte Carlo Simulation
Advanced Predictive Forecasting
Goal Seek / Optimization
Advanced xP&A
Complex Intercompany Planning/Eliminations
Statutory Consolidation
Advanced Debt / Capital Structure Modeling
Dedicated OLAP Engine
Advanced Strategic Workforce Planning
Territory/Quota Sales Planning
Advanced Zero-Based Workflow
AI Auto-Drafted Board Packs
External Macro Data Feeds
```

---
# 791. Product Preset — Tiny Company

```text
Simple Budget          ON
Actual vs Budget       ON
Simple Forecast        ON
Scenarios              2-3 simple
Budget Control         OFF/Warn
Workforce              aggregate optional
CapEx                   simple optional
```

---
# 792. Product Preset — Growing SME

```text
Department Budgets
Rolling Forecast
Scenarios
Commitments
Budget Control Warn/Approve
Workforce
CapEx
Revenue Drivers
CFO Dashboard
```

---
# 793. Product Preset — Larger Company

```text
Top-down + Bottom-up
Budget Control Block
Multi-Entity
Planning FX
Integrated Statements
Advanced allocations
SoD
Detailed variance
Long-range plan
```

---
# 794. Planning Ownership Summary

The planning module owns plans, not real-world operational events.

---
# 795. Source-of-Truth Matrix

```text
Corporate Budget / Forecast        → 28 Planning
Planning Scenario / Assumption     → 28 Planning
Budget Control Policy/Reservation  → 28 Planning
Corporate Workforce Plan           → 28 Planning
Corporate CapEx Plan               → 28 Planning
Projected Financial Statements     → 28 Planning

Actual GL / Statements             → Ledger / Accounting
Project Budget / ETC / EAC         → Projects
Treasury Cash Position/Forecast    → Banking
Purchase Commitment                → Purchasing
Employee / Employment              → HR
Payslip / Payroll Run              → Payroll
Fixed Asset / Book Depreciation    → Fixed Assets
Sales Order / Invoice              → Sales / Invoicing
Inventory Movement / Valuation     → Inventory
MRP / Production Plan              → Manufacturing
```

---
# 796. Final Actual Rule

> **Actual is imported/read from canonical source truth and is never editable planning data.**

---
# 797. Final Budget Rule

> **Approved Budget is an immutable baseline/control value; Actual, Commitment and Forecast never overwrite it.**

---
# 798. Final Forecast Rule

> **Forecast is a versioned expectation of future outcome with an explicit Actual cutoff and assumptions.**

---
# 799. Final Scenario Rule

> **What-if scenarios are isolated decision workspaces; adoption always creates a governed plan/forecast version.**

---
# 800. Final Revision Rule

> **Budget Revision changes approved authority through an auditable delta; it never rewrites Original Budget.**

---
# 801. Final Transfer Rule

> **Budget Transfer reallocates authority between buckets and conserves the total under its defined scope.**

---
# 802. Final Control Rule

> **Budget Check controls financial authority only; it never replaces Purchasing/Expense/Project business approval.**

---
# 803. Final Concurrency Rule

> **Strict budget reservations are transactional and cannot overspend the same remaining budget under concurrency.**

---
# 804. Final Commitment Rule

> **Precommitment, Commitment and Actual are separate lifecycle facts and the same economic amount cannot be double-counted.**

---
# 805. Final Override Rule

> **A transaction override is a bounded exception, not a hidden budget increase.**

---
# 806. Final Project Rule

> **Project Budget and ETC/EAC remain Project truth; FP&A aggregates them into corporate planning without creating a second project-finance ledger.**

---
# 807. Final Treasury Rule

> **Financial cash planning in `28` is strategic/forecast-oriented; bank-account liquidity and short-term treasury execution remain `25`.**

---
# 808. Final Workforce Rule

> **A planned FTE/hire is a financial/workforce planning object, not an Employee; actual payroll remains Payroll truth.**

---
# 809. Final CapEx Rule

> **A planned investment is not an Asset or Purchase Order; actual acquisition/capitalization remains source-domain truth.**

---
# 810. Final FX Rule

> **Planning FX is a scenario assumption with period/rate-type provenance and must never masquerade as actual transaction FX.**

---
# 811. Final Rollup Rule

> **Management planning rollup is not statutory consolidation unless a dedicated consolidation capability explicitly performs eliminations/accounting rules.**

---
# 812. Final Formula Rule

> **Planning calculations use deterministic versioned constrained formulas; tenants never execute arbitrary code in the planning engine.**

---
# 813. Final Lineage Rule

> **Every management number can explain source, version, period, dimensions, currency basis and calculation/override lineage.**

---
# 814. Final Spreadsheet Rule

> **Planning should feel spreadsheet-fast while preserving governed versions, validation, permissions and source-of-truth discipline.**

---
# 815. Final Small-Company Rule

> **A small company can budget and forecast without learning enterprise EPM terminology.**

---
# 816. Final Enterprise Rule

> **A larger group can add multi-entity, drivers, budget control, scenarios, allocations and integrated statements without replacing the core.**

---
# 817. Final AI Rule

> **AI can explain, suggest and simulate; it cannot approve, publish, reserve funds or replace Actual truth.**

---
# 818. Final Performance Rule

> **Interactive planning views use sparse facts/projections and targeted recalculation; do not recompute the entire model for every cell edit.**

---
# 819. Final PostgreSQL Rule

> **PostgreSQL remains the initial planning store; dedicated OLAP/cache/search infrastructure requires measured evidence and ADR.**

---
# 820. Final Product Goal

> **Replace uncontrolled budgeting spreadsheets with a planning system that remains fast enough to feel familiar and governed enough to trust.**

---
# 821. Integration Scenario — Ledger Actual Revenue

Posted revenue aggregate flows into Actual snapshot; Planning cannot edit.

---
# 822. Integration Scenario — Ledger Expense Actual

Posted expense flows to mapped OPEX Actual.

---
# 823. Integration Scenario — Accounting Period Close

Forecast Actual cutoff can advance after close according to policy.

---
# 824. Integration Scenario — Purchasing PR

Approved requisition may create precommitment.

---
# 825. Integration Scenario — Purchasing PO

Approved PO replaces/creates commitment.

---
# 826. Integration Scenario — Supplier Invoice

Actual recognition reduces open PO commitment.

---
# 827. Integration Scenario — PO Price Increase

Additional commitment requires budget recheck.

---
# 828. Integration Scenario — PO Quantity Decrease

Unused commitment releases.

---
# 829. Integration Scenario — Expense Claim

Approved expense checks/consumes budget according to policy.

---
# 830. Integration Scenario — Project Budget

Detailed project plan remains Project-owned and aggregates to corporate envelope.

---
# 831. Integration Scenario — Project EAC

Corporate forecast consumes Project EAC read-only.

---
# 832. Integration Scenario — Project Change Order

Updated Project forecast rolls to FP&A without rewriting approved corporate budget automatically.

---
# 833. Integration Scenario — Payroll Finalization

Aggregate payroll actual updates Actual snapshot.

---
# 834. Integration Scenario — New-Hire Plan

Workforce planned hire creates no HR Employee.

---
# 835. Integration Scenario — HR Headcount

Actual headcount sourced as-of from HR.

---
# 836. Integration Scenario — Fixed Asset Addition

Actual CapEx/addition refreshes plan-vs-actual.

---
# 837. Integration Scenario — Fixed Asset Depreciation

Actual depreciation sourced from asset/accounting, not planning assumption.

---
# 838. Integration Scenario — Manufacturing Cost Increase

Cost projection may feed scenario; no BOM mutation.

---
# 839. Integration Scenario — MRP Volume

Production plan may inform operational scenario; no Production Order duplication.

---
# 840. Integration Scenario — Inventory Cost

Actual inventory/cost source informs working-capital/COGS drivers as defined.

---
# 841. Integration Scenario — Sales Pipeline

CRM/Sales pipeline can seed forecast but remains source-owned.

---
# 842. Integration Scenario — Sales Order

Order commitment may appear as operational indicator, not recognized Actual revenue.

---
# 843. Integration Scenario — Invoicing Revenue

Invoice can be operational feed while Accounting determines recognized revenue Actual.

---
# 844. Integration Scenario — Subledger DSO

Receivables actual/aging can inform working-capital assumptions/forecast.

---
# 845. Integration Scenario — Subledger DPO

Payables actual/aging can inform working-capital assumptions/forecast.

---
# 846. Integration Scenario — Banking Cash Position

Current Treasury cash may be displayed for management comparison.

---
# 847. Integration Scenario — Banking 30-Day Forecast

Short-term treasury forecast remains Banking; FP&A may compare high-level trend.

---
# 848. Integration Scenario — Multi-Currency Entity

Entity plan converted using Planning FX.

---
# 849. Integration Scenario — Group Rollup

Authorized entities aggregate to management currency.

---
# 850. Integration Scenario — Budget Transfer Across Departments

Transfer conserves company total.

---
# 851. Integration Scenario — Budget Revision Additional Funding

Revision changes current budget, keeps original baseline.

---
# 852. Integration Scenario — Department Submission

Only assigned planning slice editable.

---
# 853. Integration Scenario — Returned Submission

Manager edits after Finance sends back.

---
# 854. Integration Scenario — Forecast From Budget

Future periods seeded from budget, then planner updates.

---
# 855. Integration Scenario — Forecast From Run Rate

Method and formula visible.

---
# 856. Integration Scenario — Scenario From Forecast

Clone preserves source version and becomes isolated.

---
# 857. Integration Scenario — Scenario Adopted

Creates new draft Forecast version.

---
# 858. Integration Scenario — FX Stress

Only stress scenario rates change.

---
# 859. Integration Scenario — Inflation Stress

OPEX/material driver assumptions update dependent plan.

---
# 860. Integration Scenario — Sales Downside

Revenue/working capital/cash effects flow through integrated plan.

---
# 861. Integration Scenario — Delayed Branch Opening

Revenue, rent, hiring and CapEx dates shift in scenario.

---
# 862. Integration Scenario — Headcount Freeze

Future hires removed from scenario; no HR changes.

---
# 863. Integration Scenario — CapEx Delay

Planned cash/depreciation shifts; Asset unchanged.

---
# 864. Integration Scenario — Budget Block

Source transaction receives Block; no reservation.

---
# 865. Integration Scenario — Budget Warn

Source can continue according to owning workflow but warning/audit recorded.

---
# 866. Integration Scenario — Budget Approval Required

Source workflow requests configured override/approval.

---
# 867. Integration Scenario — Override Approved

One source transaction can proceed; baseline unchanged.

---
# 868. Integration Scenario — Override Rejected

Source remains blocked.

---
# 869. Integration Scenario — Commitment Stale

FP&A flags source owner; cannot cancel source itself.

---
# 870. Integration Scenario — Actual Restatement

Planning actual refresh shows restated source with updated watermark.

---
# 871. Integration Scenario — Published Forecast Correction

New version supersedes prior without deletion.

---
# 872. Integration Scenario — Excel Import

Validated rows update draft only.

---
# 873. Integration Scenario — Excel Bad Member

Import rejects/maps unknown dimension explicitly.

---
# 874. Integration Scenario — Large Paste

Batch write preserves atomic scope and concurrency token.

---
# 875. Integration Scenario — Allocation By Headcount

HQ cost distributed using authorized headcount driver.

---
# 876. Integration Scenario — Allocation Driver Change

Recalc creates new draft result; published result unchanged.

---
# 877. Integration Scenario — Seasonal Spread

Annual total preserved after month weights.

---
# 878. Integration Scenario — Long-Range Annual

5-year plan uses annual grain without monthly explosion.

---
# 879. Integration Scenario — 13-Period Calendar

Forecast/budget maps to legal-entity periods correctly.

---
# 880. Integration Scenario — Arabic Grid

RTL presentation does not change coordinate/value semantics.

---
# 881. Final Forecast Version Rule

Published Forecast versions remain queryable and comparable after newer forecasts publish.

---
# 882. Final Target Rule

Target is directional/aspirational and never silently treated as approved spending authority.

---
# 883. Final Driver Rule

Driver values are first-class versioned inputs with scope and units.

---
# 884. Final Assumption Rule

Every major scenario assumption has owner, period, scope and version.

---
# 885. Final Actual Cutoff Rule

Every Forecast says exactly through which period Actual is included.

---
# 886. Final Source Feed Rule

Operational source forecast is read-only input unless planner intentionally overrides in a draft version.

---
# 887. Final Override Visibility Rule

Override never hides model-generated value; both remain inspectable.

---
# 888. Final Variance Rule

Variance sign and favorable/unfavorable semantics are metric-specific and documented.

---
# 889. Final Materiality Rule

Commentary/approval requirements use explicit materiality thresholds.

---
# 890. Final Forecast Accuracy Rule

Accuracy compares frozen prior forecast to later Actual using named formula.

---
# 891. Final Budget Availability Rule

Available Budget is derived; no user edits the available number.

---
# 892. Final Commitment Release Rule

Cancellation/decrease releases budget exactly once.

---
# 893. Final Partial Actual Rule

Partial Actual replaces only corresponding portion of commitment.

---
# 894. Final Control FX Rule

Budget-control FX basis is explicit and reproducible.

---
# 895. Final Strict Control Health Rule

Strict blocking control cannot silently operate from stale/unreconciled projection.

---
# 896. Final Shadow Mode Rule

New strict controls should support pre-go-live advisory validation.

---
# 897. Final Submission Rule

Submitting a slice locks the same version/scope, not unrelated departments.

---
# 898. Final Approval Rule

Approving plan version does not approve downstream PO/Expense/Project transactions.

---
# 899. Final Planning Period Rule

Fiscal period identity is canonical; display month name is not enough.

---
# 900. Final Multi-Currency Rule

Never aggregate mixed currencies without explicit conversion.

---
# 901. Final Planning Statement Rule

Projected statements are forecasts, not legal/statutory Actual statements.

---
# 902. Final Balance Rule

Integrated Balance Sheet must balance or explicitly report model incompleteness.

---
# 903. Final Cash Rule

Projected cash reconciles to financial plan; bank liquidity remains Treasury-owned.

---
# 904. Final Workforce Privacy Rule

Individual compensation is not required for standard FP&A headcount planning.

---
# 905. Final CapEx Depreciation Rule

Planned depreciation is scenario/model output; asset-book depreciation remains Fixed Assets truth.

---
# 906. Final Project Portfolio Rule

Corporate envelopes may control project portfolio without duplicating Project execution finance.

---
# 907. Final Excel Rule

Excel import/export never bypasses validation, authorization, versioning or audit.

---
# 908. Final Bulk Edit Rule

Bulk actions are server-validated and audit summarized without hiding affected scope.

---
# 909. Final Private Version Rule

Private scenario cannot be used for official Budget Control.

---
# 910. Final Published Budget Control Rule

Only configured approved/published budget versions feed strict control.

---
# 911. Final Control Version Switch Rule

Changing active control budget/version is a governed effective-dated action.

---
# 912. Final Historical Control Rule

Past budget decisions retain policy/version used at decision time.

---
# 913. Final Audit Rule

Critical plan/control actions retain who/when/what/why.

---
# 914. Final Correction Rule

Corrections are semantic/versioned, not database overwrites.

---
# 915. Final Migration Rule

Do not invent history absent from legacy budget files.

---
# 916. Final Performance Rule — Grid

Never fetch one API/query per visible cell.

---
# 917. Final Performance Rule — Calculation

Recalculate dependency impact, not whole tenant by default.

---
# 918. Final Performance Rule — Budget Control

Hot budget buckets use transactional projection and measured contention handling.

---
# 919. Final Security Rule — Drilldown

Summary access does not imply source transaction access.

---
# 920. Final Security Rule — Scenarios

Confidential scenarios can be isolated from ordinary planners/admins as configured.

---
# 921. Final Security Rule — Exports

Export is permissioned and auditable.

---
# 922. Final Regional Rule — FX

FX/inflation assumptions are easy to scenario-test but never hardcoded by TODIR.

---
# 923. Final Regional Rule — Multi-Entity

Egypt/GCC/UAE entity planning shares core while each Legal Entity remains separate truth.

---
# 924. Final Regional Rule — Arabic

Arabic/RTL is a first-class dense-grid UX requirement.

---
# 925. Final SME Rule

Company can stay with a few categories and months; unused advanced dimensions stay hidden.

---
# 926. Final Scale Rule

Large enterprise can expand dimensions/workflows without data-model fork.

---
# 927. Final Agent Rule — Source

Before creating any FP&A fact, ask whether another module already owns the real-world fact.

---
# 928. Final Agent Rule — Provenance

Every derived financial result must expose basis/version/source.

---
# 929. Final Agent Rule — Budget

Never implement Budget as a mutable total field.

---
# 930. Final Agent Rule — Forecast

Never implement Forecast as the latest mutable row only.

---
# 931. Final Agent Rule — Scenario

Never allow scenario edits to share physical records with approved plan in a way that can leak changes.

---
# 932. Final Agent Rule — Control

Never make a blocking control dependent only on async eventual consistency if overspend can occur.

---
# 933. Final Agent Rule — UX

Do not expose implementation dimensions/IDs to ordinary planner.

---
# 934. Final Agent Rule — Simplicity

Prefer safe defaults, seeding and inferred context over repetitive entry.

---
# 935. Final Agent Rule — Explainability

If Finance cannot reproduce why a number changed, the implementation is incomplete.

---
# 936. Final Agent Rule — Reconciliation

Every Actual/Commitment integration requires a reconciliation story.

---
# 937. Final Agent Rule — History

Approved/plublished history must survive master-data/formula changes.

---
# 938. Final Agent Rule — Test

Golden-vector tests required for formulas, spreads, FX and budget availability.

---
# 939. Final Agent Rule — Database

No bypass of RLS/transaction semantics for batch calculation jobs.

---
# 940. Final Agent Rule — AI

AI suggestions go through ordinary authorization/command boundaries.

---
# 941. Final Agent Rule — Release

No release because screens look complete; release requires finance/control acceptance evidence.

---
# 942. Final Agent Question

For every `28` implementation decision, the Agent must answer:

> **Which version/scenario is this number in, what period and dimensions does it represent, is it Actual/Budget/Forecast/Target/Scenario, what source or assumption created it, what currency basis applies, is it entered/calculated/allocated/overridden, who may change it, and does another TODIR module own any underlying real-world fact?**

If those answers are unclear, implementation is incomplete.

---
# 943. Architecture Closure

TODIR `28` must remain:

```text
Simple outside.
Powerful inside.
Versioned.
Explainable.
Scenario-ready.
Budget-control safe.
Source-owned.
Spreadsheet-fast.
Multi-currency aware.
History preserving.
```

A beautiful FP&A dashboard with mutable baselines, stale control, duplicated Project/Payroll/Treasury truth or unexplained numbers is a failed implementation.

---
# 944. No Separate FP&A Subfeature Packs

Do **not** create separate architecture packs for:

```text
Annual Budget
Rolling Forecast
Scenario Planning
Revenue Planning
OPEX Planning
Workforce Planning
CapEx Planning
Budget Control
Allocations
Integrated Financial Plan
```

They belong inside `28`. Only a truly separate future bounded context such as statutory Consolidation should get its own pack.

---
# 945. Final Release Summary

TODIR FP&A is release-ready only when:

```text
Approved Budget cannot be silently rewritten.
Actual is canonical and read-only.
Forecast versions preserve history.
Scenarios are isolated.
Drivers/formulas are versioned and explainable.
Spreadsheet entry is fast.
Budget revisions/transfers are explicit.
Commitments do not double-count.
Strict budget control is concurrency-safe.
Project/Treasury/HR/Payroll/Assets remain source-owned.
Multi-currency basis is explicit.
Management rollup is not mislabeled consolidation.
Variance and forecast accuracy are reproducible.
CFO/FP&A/Department acceptance passes.
```

---

**END OF FILE — 28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
