# TODIR Commercial, CRM & Sales Domain Pack

Canonical specifications:
- `docs/modules/19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `docs/modules/17_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Modules: `crm`, `sales`, `pricing`, `directory`

---

## Core Entities & Workflows

1. **Lead Lifecycle (`العميل المحتمل`)**:
   - States: `New -> Working -> Qualified -> Disqualified / Converted`.
   - Centered Modal Dialogs for:
     - New Lead capture.
     - Qualify Lead (BANT criteria: Budget, Authority, Need, Timeline).
     - Disqualify Lead (mandatory reason).
     - Convert Lead (creates Commercial Party + Pipeline Opportunity; user enters authentic deal amount, no fabricated defaults).
     - Duplicate Merge.
2. **Opportunities Pipeline (`فرص المبيعات`)**:
   - Kanban Board with drag-and-drop / stage selector, probability-weighted stage sums, and list toggle.
   - Distinct deal health badges: `Healthy`, `Good`, `Fair`, `AtRisk`, `Stale` (>14 days without activity).
   - Opportunity 360 Workspace: Buying Committee stakeholder matrix, commercial interests tracking, and Quotation handoff.
3. **Sales Metrics & Forecasting**:
   - **Opportunity Win Rate** (`reportsSummary.opportunityWinRate`): Won deals / total closed deals.
   - **Lead Conversion Rate** (`reportsSummary.leadConversionRate`): Converted leads / total leads.
   - Zero-denominator boundary: strictly `0.00%` when denominator is 0.
   - Revenue forecasts across Pipeline, Best Case, and Commit categories.
