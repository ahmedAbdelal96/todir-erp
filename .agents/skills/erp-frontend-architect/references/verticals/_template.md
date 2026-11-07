# TODIR Module Domain Pack: <Module Name>

Template for documenting canonical business modules under `references/verticals/<module-name>.md` (e.g. `manufacturing`, `fixed-assets`, `pos`, `reconciliation`). Grounded in `PRODUCT_ENGINEERING_PRINCIPLES.md` and `docs/modules/*.md`.

---

## 1. Actors & Operational Context
| Role | Operating Context & Pressure | The Single Primary Job on this Screen |
|---|---|---|
| Accountant | Month-end close, financial audits | Verify debit/credit balance, review exceptions, post entries |
| Warehouse Clerk | High-frequency physical dock operations | Fast barcode scanning, physical receipt confirmation, bin assignment |
| Sales Rep | On-the-go commercial meetings | Quick lead capture, pipeline status updates, quote handoffs |

---

## 2. Core Business Lifecycle
1. `DRAFT`: Editable, deletable, no general ledger or inventory impact.
2. `SUBMITTED / PENDING`: Locked for approval, pending manager authorization.
3. `APPROVED / POSTED`: Strictly **immutable**, posts to ledger/inventory. Corrections require formal credit notes or reversals.
4. `CANCELLED / REVERSED`: Audit-trailed reversal with mandatory business reason.

---

## 3. Domain Glossary & Natural Arabic Vocabulary
| Prohibited Developer / Literal Term | Authoritative Arabic Business Term | Context & Notes |
|---|---|---|
| Lead / ليد | عميل محتمل | CRM / Commercial |
| Journal Entry / إدخال اليومية | قيد يومية | General Ledger |
| Goods Receipt / إيصال سلع | إذن استلام بضاعة | Stocked items only |
| Delivery Note / مذكرة تسليم | إذن صرف / تسليم بضاعة | Stock movements |
| Reversal / إرجاع | قيد عكسي / إلغاء تسوية | Accounting adjustments |

---

## 4. UI Invariants & Guardrails
- **Immutable Records**: Delete button hidden for approved records; replacement action is Reversal/Adjustment with mandatory reason.
- **Service vs Stock Invariant**: Non-stock and service lines must NEVER appear in inventory receipts or require warehouse selection.
- **Missing Amounts**: Missing or null financial figures render as em-dash (`"—"`), never as `0.00`.
- **Contrast Standard**: Oxford Navy header (`#102a43`), slate canvas (`#f0f4f8`), pure white cards (`#ffffff`) with borders (`#cbd5e1`).

---

## 5. Headline Numbers (Rule of Three)
| Operational View | Number 1 (Planned/Target) | Number 2 (Actual/Realized) | Number 3 (Variance/Remaining) |
|---|---|---|---|
