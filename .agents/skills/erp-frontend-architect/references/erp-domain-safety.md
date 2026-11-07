# TODIR ERP Domain Safety & Business Invariants

This manual codifies the non-negotiable domain rules derived from `/PRODUCT_ENGINEERING_PRINCIPLES.md`. A frontend violation of these rules becomes an accounting, legal, or inventory corruption defect.

---

## 1. Monetary Precision & Arithmetic Invariants

1. **Zero Floating-Point Math**:
   - JavaScript `Number` cannot represent decimal fractions accurately (`0.1 + 0.2 === 0.30000000000000004`).
   - All financial amounts travel between API and UI as exact decimal strings (e.g. `"1250.50"`).
   - The backend is the single authority for totals, tax calculations, discounts, and currency revaluations. Never calculate tax or document totals on the frontend and send them as authority.
2. **Missing vs Zero Invariant**:
   - `0.00` is a valid financial quantity (e.g. Zero-Rated Tax, or 0 balance).
   - `null` or `undefined` means "Not Set" or "Missing Data".
   - The UI must render missing amounts as an em-dash (`"—"`), never as `0.00`.
3. **Currency Invariants**:
   - Never hardcode a currency code (`"EGP"`, `"SAR"`, `"USD"`).
   - Displayed currency must come from the backend DTO (`currencyCode`) or active legal entity base currency.

---

## 2. Immutability of Posted Records

1. **Draft vs Posted**:
   - **Draft records (`DRAFT`)**: Can be edited and permanently deleted.
   - **Approved / Posted records (`POSTED`, `FINALIZED`, `CONFIRMED`)**: Strictly **immutable**.
2. **Correction Path**:
   - Once a journal entry is posted, an invoice finalized, or a stock movement confirmed, the UI must **never** offer a Delete button.
   - The UI offers the business-proper correction mechanism:
     - Posted Invoice -> **Credit Note (`إشعار دائن`)** or **Debit Note (`إشعار مدين`)**.
     - Posted Journal -> **Reversal (`عكس القيد`)**.
     - Confirmed Stock Movement -> **Inventory Adjustment (`تسوية مخزنية`)**.
3. **Mandatory Reason Capture**:
   - Any reversal or correcting adjustment dialog must require an explicit Arabic business reason that gets recorded in the backend Audit Trail.

---

## 3. Supply Chain & Inventory Tracking Invariants

1. **Physical Receipt Eligibility**:
   - Goods Receipts (`إذن استلام بضاعة`) are strictly for physical, stock-tracked goods.
   - **Service Lines & Non-Stock Items**:
     - Must NOT receive an inventory quantity.
     - Must NOT require a Warehouse or Location.
     - Must NOT create stock movements.
     - Must NOT appear in physical receipt dialogs.
   - The frontend must never classify stock eligibility via client heuristics like `productVariantId != null`. Eligibility comes strictly from authoritative backend catalog/inventory classification (`isStocked`, `receiptEligible`, `lineType`).

---

## 4. Multi-Tenant Isolation & Cache Invariants

1. **Tenant ID is an Internal Security Boundary**:
   - The client never selects, sends, or modifies `TenantId`. Tenant identity is bound server-side from the authenticated session.
   - `LegalEntityId` is passed explicitly only when operating in multi-company business workflows.
2. **Cache Eviction on Context Switch**:
   - When a user logs out, switches company/legal-entity, or switches tenant, the entire TanStack Query cache must be cleared immediately (`queryClient.clear()`).
   - Cross-tenant data leakage in UI caches is a critical security violation.

---

## 5. Concurrency & Idempotency

1. **Idempotency Keys**:
   - All mutating operations (creating orders, posting payments, qualifying leads) must send an `Idempotency-Key` HTTP header (UUID v7 or client-generated unique key).
2. **Double-Click Protection**:
   - Primary action buttons must immediately enter a disabled/loading state on the first click to prevent duplicate submissions.
3. **Optimistic Concurrency**:
   - Documents carrying a `version` or `rowVersion` field must send it back on update.
   - On concurrency conflict (`409 Conflict`), display a clear Arabic message: `تم تحديث هذا السجل بواسطة مستخدم آخر. يرجى تحديث البيانات.` without losing unsaved form inputs if possible.
