# TODIR Supply Chain, Inventory & Purchasing Domain Pack

Canonical specifications:
- `docs/modules/18_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Modules: `inventory`, `purchasing`, `catalog`

---

## Core Entities & Workflows

1. **Purchase Orders (`أمر شراء`)**:
   - States: `DRAFT -> SUBMITTED -> APPROVED -> RECEIVING -> COMPLETED / CANCELLED`.
   - Distinct line types: Stocked Goods (`isStocked = true`) vs Non-Stock / Services.
2. **Goods Receipt Invariant (`إذن استلام بضاعة`)**:
   - Goods Receipt is strictly for physically receivable, inventory-tracked items.
   - **Service lines & non-stock items must NEVER:**
     - Appear as physically receivable in Goods Receipt dialogs.
     - Require Warehouse or Location inputs.
     - Receive inventory quantities or generate stock movement records.
   - Eligibility derives from backend authoritative catalog classification (`isStocked`, `receiptEligible`), never client heuristics (`productVariantId != null`).
3. **Inventory Movements & Stock Valuation**:
   - Confirmed movements are immutable and post inventory valuation journals.
   - Discrepancies resolved strictly through formal **Inventory Adjustments (`تسوية مخزنية`)**.
