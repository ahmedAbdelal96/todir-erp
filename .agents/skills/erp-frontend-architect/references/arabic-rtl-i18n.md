# TODIR ERP Arabic-First, RTL-Native & Localization Standards

In TODIR ERP, Arabic is the primary market language and RTL is the foundational layout mode, not a mirrored afterthought. Misaligned punctuation, scrambled codes, or literal machine translations destroy enterprise trust.

---

## 1. CSS Standard Logical Properties

Never use physical `left` or `right` directions in styles. Always use modern CSS logical properties:

| Avoid (Physical) | Mandated (Logical) | Behavior in RTL / LTR |
|---|---|---|
| `marginLeft` | `marginInlineStart` | Right in RTL, Left in LTR |
| `marginRight` | `marginInlineEnd` | Left in RTL, Right in LTR |
| `paddingLeft` | `paddingInlineStart` | Right in RTL, Left in LTR |
| `paddingRight` | `paddingInlineEnd` | Left in RTL, Right in LTR |
| `left: 0` | `insetInlineStart: 0` | Right edge in RTL, Left in LTR |
| `right: 0` | `insetInlineEnd: 0` | Left edge in RTL, Right in LTR |
| `textAlign: "left"` | `textAlign: "start"` | Aligns with natural reading direction |
| `textAlign: "right"` | `textAlign: "end"` | Aligns with opposite reading direction |
| `borderLeft` | `borderInlineStart` | Leading border |
| `borderRight` | `borderInlineEnd` | Trailing border |

---

## 2. Direction-Aware Component & Icon Behavior

- **Directional Icons (Must Flip in RTL)**:
  - Navigation arrows, chevrons (`ChevronRight` becomes leading back arrow), "Send", breadcrumbs separators, and back/forward buttons must be rotated 180 degrees or flipped via CSS `transform: scaleX(-1)`.
- **Non-Directional Icons (Must NEVER Flip)**:
  - Checkmarks, clocks, search magnifiers, status badges, currency symbols, and phone icons must never flip.
- **Drawers & Modals**:
  - Side Drawers slide in from `inline-start` (the right edge in RTL).
  - Steppers and progress timelines advance from right to left (`start -> end`).

---

## 3. Mandatory Bidirectional Text Isolation (`<BdiText>` / `<bdi>`)

When Latin identifiers, numbers, dates, or emails appear within Arabic text, the Unicode Bidirectional Algorithm can scramble punctuation (e.g. turning `(INV-2026-001)` into `-001)INV-2026(`).

**Rules:**
1. Wrap all reference codes, invoice numbers, SKUs, phone numbers, units, and emails inside `BdiText` (`frontend/components/formatting/bidi-text.tsx`) or `<bdi>`:
   ```tsx
   import { BdiText } from "frontend/components/formatting/bidi-text";

   <span>
     {t("invoicing.invoiceNumber")}: <BdiText>{invoice.number}</BdiText>
   </span>
   ```
2. Never concatenate translated fragments around variables using `+`. Always use ICU-style interpolation:
   - Correct: `t("crm.lead.assignedTo", { name: user.name })` -> `"تم التعيين إلى {{name}}"`
   - Prohibited: `t("crm.lead.assignedTo") + " " + user.name`

---

## 4. Central Formatting & Currency Invariants

All numbers, monetary amounts, percentages, and dates must be formatted strictly through:
`frontend/components/formatting/quantity-formatter.ts`

```tsx
import { formatCurrency, formatQuantity, formatPercentage, formatDate } from "frontend/components/formatting/quantity-formatter";

// Money formatting
formatCurrency(invoice.totalAmount, invoice.currencyCode); // "1,250.00 EGP" or "1,250.00 ر.س"

// Null/Missing Money Invariant:
formatCurrency(null, "EGP"); // Returns "—" (em-dash), never "0.00"

// Quantity formatting with unit:
formatQuantity(150, "طن"); // "150 طن"

// Percentage formatting:
formatPercentage(0.85); // "85.00%"
```

**Financial Rules:**
- Never use `.toFixed(2)` or manual string template concatenation for money.
- Currency code must derive authoritatively from backend DTO (`currencyCode`). Never hardcode `SAR` or `EGP`.
- Digits: Use Latin digits (`0-9`) in data tables and financials with Western grouping separators (`,` and `.`) for uniform column alignment.

---

## 5. 100% Arabic & English Key Parity

1. Every business module owns `modules/<module>/i18n/ar.ts` and `en.ts`.
2. Both files must have **identical keys** with zero missing translations.
3. Every screen must undergo automated verification via `pnpm run localization:verify`.
4. Hardcoded Arabic or English text in JSX is strictly forbidden.

---

## 6. Authoritative Enterprise Arabic Vocabulary

| Concept | Market Arabic Term (Mandatory) | Prohibited Literal / English |
|---|---|---|
| General Ledger | دفتر الأستاذ العام | الليدجر / سجل الحسابات |
| Journal Entry | قيد يومية | إدخال يومي / Journal |
| Debit / Credit | مدين / دائن | خصم / إضافة |
| Trial Balance | ميزان المراجعة | توازن التجربة |
| Accounts Payable | حسابات الموردين (ذمم دائنة) | الدفعيات / AP |
| Accounts Receivable | حسابات العملاء (ذمم مدينة) | المقبوضات / AR |
| Purchase Order | أمر شراء | طلب شراء / PO |
| Goods Receipt | إذن استلام بضاعة | إيصال سلع / استلام مخزني |
| Delivery Note | إذن صرف / تسليم بضاعة | مذكرة تسليم |
| Sales Quotation | عرض أسعار | تسعيرة / Quote |
| Opportunity | فرصة مبيعات | فرصة تجارية |
| Lead | عميل محتمل | عميل متوقع / ليد |
| Win Rate | معدل الفوز بالصفقات | معدل الربح |
| Lead Conversion Rate | معدل تحويل العملاء | نسبة التحويل |
| Credit Note | إشعار دائن | مذكرة ائتمان |
| Debit Note | إشعار مدين | مذكرة خصم |
| Reversal | قيد عكسي / إلغاء تسوية | إرجاع / استرجاع |
