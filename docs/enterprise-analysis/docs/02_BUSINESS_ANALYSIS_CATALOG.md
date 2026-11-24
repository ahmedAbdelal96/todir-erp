# 02 — Candidate Business Analysis Catalog (Not a Build-All Backlog)

**Purpose:** Useful Egyptian business questions first, then their filters/facts/outputs/recommendations. These are **candidate product requirements**, NOT claims that source contracts, data quality or model accuracy already exist. Promotion into committed backlog requires code evidence + source owner + business validation + test cases.

## Registry entry template — mandatory per use case

`analysis_code` (stable internal key), Arabic business label, owning module, target persona, decision question, data time basis, period applicability, eligible filters, source owner/contracts, calculation definitions/formulas, currencies/units, exception and return handling, data-readiness metrics, expected output shapes, candidate recommendations, permissions, performance limits, backtest requirements (when forecasting), UX states, precise acceptance fixtures.

## Prioritized candidates — domain-specific, not marketing terms

| Stage | Module / analysis page | Candidate analysis / real decision | Input scope and filters | Required authoritative facts | Potential recommendation; never auto-execute |
|---|---|---|---|---|---|
| P3 candidate | Subledger/Payments — **تحليل تحصيلات العملاء** | Overdue aging; payment allocation delays; customer collection history | Legal entity; as-of date; period for cash; customer/team; currency; overdue band | AR open items, applications, actual payment completion, credit notes/write-offs/reversals | Prioritize follow-up on customers whose exposure and historical delays justify it; flag unallocated receipts |
| P4 | Sales — **تحليل المبيعات** | Sales by products/customers/branches; growth/change from comparison period | From/to; invoice vs order basis; branch, product group, customer, currency | Sales actuals with returns/cancellations and posting basis | Review declining product group or unusually high returns; no unverified pricing advice |
| P4 | Inventory — **تحليل المخزون** | Slow movement, shortages and overstocks, physical vs available | Warehouse, product/category, date interval, UOM, as-of | Stock ledger, inventory balance/valuation, reserved and inbound evidence | Review reorder/stock-transfer candidate; never reserve/transfer automatically |
| P5 | Purchasing — **تحليل المشتريات** | Supplier lead-time and purchase-price deviation | Supplier, category, legal entity, date period, currency, receiving stage | Approved POs, receipts, agreed terms/pricing, reversals | Review supplier reliability, investigate cost changes |
| P5 | Banking — **تحليل السيولة** | Cash exposure from confirmed facts and what-if shortfalls | Cash accounts, legal entity, as-of, forecast horizon, currency | Bank/cash balances, statement match confidence, AR/AP obligations | Review collections/payments timing; not override Banking cash-forecast version |
| P5 | Accounting — **التحليل المالي** | Variances in margins/costs, period comparisons | Legal entity, financial period, reporting basis, currency | Posted ledger, P&L, TB, close state | Investigate anomaly with journal evidence; no fabricated causes |
| P5 | CRM — **تحليل فرص البيع** | Pipeline conversion and stale opportunities | Pipeline, salesperson, stage, period | Opportunities and stage history, activities, closed outcomes | Follow up with neglected opportunity; don't call weighted pipeline calculation ML |
| P5 | POS — **تحليل مبيعات نقاط البيع** | Sales mix, returns, session/cash reconciliation trends | Store, cashier if allowed, shifts, date period | Closed sessions, sales and refunds, settlement state | Investigate unusually high returns or till variance |
| P6 | Manufacturing — **تحليل الإنتاج** | Production cost/scrap/lead time deviation by work center/order | Plant, order, product, period, approved BOM version | Confirmed production events, actual costs, genealogy/quality gates | Investigate high scrap/downtime; never publish planned MRP as Actual |
| P6 | Projects — **تحليل تكاليف المشروعات** | Budget/actual progress and margin variance | Project, work package, fiscal period | Project costs, approved commitments and posted accounting facts | Review project cost overruns; ownership with Projects and Accounting |
| P6 | HR/Payroll — **تحليل تكاليف العاملين** | Payroll trends, overtime and department cost | Legal entity, department, payroll run, period | Authorized payroll and HR facts | Review workforce cost changes; sensitive aggregated access only |
| P6 | Service — **تحليل أعمال الصيانة** | Work order responsiveness, repeat incidents, cost | Team, category, service period | Approved work orders, timesheets, materials | Review repeat repairs and allocation of teams |
| P6 | Expenses — **تحليل المصروفات** | Expense policy exceptions and category growth | Policy/category, period, branch | Approved expense facts and reimbursements | Review recurring exceptions without bypassing approvals |
| P6 | Fixed Assets — **تحليل الأصول الثابتة** | Asset service/carrying value trends and disposal cases | Asset class, site, period | Owned asset book, posted depreciation/disposals | Review underutilized assets; no automatic disposal |

### Lower-priority domain owners, not automatic analysis projects

`Directory`, `Catalog`, `FinBase`, `Tax`, `Pricing`, `Ledger`, `Invoicing` and other financial kernels may provide source facts/analytics contracts or already own operational reports. **Do not invent separate standalone analysis pages for each.** The requirement is “for each justified business use case, inside its relevant owning module,” not “an analysis menu for every technical module.”

`Quality`, `Budgeting/FP&A`, `Helpdesk`, `Subscriptions`, `Channels`, `Logistics` have design packs in `docs/modules`, but no corresponding backend implementation directory was seen at pinned SHA. Their prospective analyses remain **FUTURE** pending implementation and runtime verification; no phantom menu entries.

## Definition and collision policy

- Analysis of current sales ≠ sales orders count; define `booked`, `invoiced`, `posted`, `net-of-returns`, `tax-inclusive/exclusive` as distinct metrics. No silently mixed bases.
- Treasury liquidity projection ≠ approved FP&A Forecast; don't create conflicting official forecasts.
- MRP planning is a domain planning capability; Analysis may interpret completed MRP runs, never create an alternative MRP engine under a generic AI layer.
- Existing `Subledger` application suggestions, Banking reconciliation suggestions and CRM pipeline snapshots are owner-domain features. New analysis must **consume** them if needed, not replace their rules.
- Money by currency must not be summed across currencies without explicit FX source/effective date, and FX-sensitive conclusions show exposure.
- HR/Payroll records must be aggregate/masked by role. No broad cross-entity people analysis via executive access alone.

## Promotion scorecard (0–5 each, proof required)

`Problem severity`, `decision frequency`, `source truth maturity`, `expected commercial value`, `measurability`, `performance cost`, `privacy impact`, `reusability`. A use case with unverified source truth or excessive permission ambiguity is **blocked** regardless of score. Score is a prioritization aid, not an automatic release decision.

## Detailed definition for first candidate: Collections Hygiene

- **Question:** Which money is genuinely overdue, which collections have been received but not allocated, and where should responsible staff focus follow-up?
- **Required form:** legal entity, overdue `as_of`, optional activity from/to, currency, customer(s), bucket/delay threshold, include/exclude opening balances, metric basis documented.
- **Metrics:** outstanding AR (by currency); overdue by due date; allocated cash receipts; noncash settlements; unapplied customer receipt aging; writeoff/reversal count; payment-date vs apply-date delay; insufficient-confidence count.
- **Recommendation rules candidate:** `review unallocated receipt`; `review persistent overdue exposure`; `investigate data anomaly`; not `block customer`, `change credit limit` or `write off` automatically.
- **Evidence:** exact open-item IDs / source reference and summarized payment/application linkage, as-of and knowledge cutoff; owner-scope checked at view-time.
- **Data gate:** classify payment methods and proof of received cash, verify partials and historical as-of. If not possible, restrict output to descriptive truth and warnings.
- **No invented ML metric:** all v1 calculations deterministic and evaluated against controlled expected outcomes.

## Analysis template approval checklist

- [ ] Relevant owner identifies canonical business facts and negative cases (void, reversal, backdated, partial).
- [ ] Arabic label tested with real target user; no AI jargon.
- [ ] Types, period semantics, filter dependencies and default values reviewed.
- [ ] Clear recommendation eligibility and prohibition conditions.
- [ ] Data readiness check and practical performance budget.
- [ ] Permissions include visibility/read/run/decision, tenant/entity/branch/record scope.
- [ ] Owner module docs and regression tests remain compatible.
