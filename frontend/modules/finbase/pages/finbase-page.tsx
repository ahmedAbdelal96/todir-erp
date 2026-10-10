import { useEffect, useState } from "react";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { finbaseApi } from "../services/finbase-api.ts";
import type { Currency, PaymentScheduleLine } from "../types.ts";

export function FinBasePage(): React.JSX.Element {
  const [currencies, setCurrencies] = useState<readonly Currency[]>([]);
  const [money, setMoney] = useState({ amount: "100.00", currency: "EGP" });
  const [schedule, setSchedule] = useState<readonly PaymentScheduleLine[]>([]);
  const [error, setError] = useState<string>();
  useEffect(() => { void finbaseApi.currencies().then(setCurrencies).catch(() => setError("تعذر تحميل مرجع العملات")); }, []);
  async function preview() {
    setError(undefined);
    try { setSchedule(await finbaseApi.previewSchedule(money.amount, new Date().toISOString().slice(0, 10), [{ sequence: 1, allocationType: "Percentage", percentage: 33.33, dueDateBasis: "InvoiceDate", days: 0, monthOffset: 0 }, { sequence: 2, allocationType: "Balance", dueDateBasis: "InvoiceDate", days: 30, monthOffset: 0 }])); } catch { setError("تعذر حساب جدول الاستحقاق"); }
  }
  return <section style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "1.5rem" }}>
    <div><p className="eyebrow">FinBase</p><h1 style={{ margin: 0 }}>الأساس المالي</h1><p style={{ color: "var(--color-content-muted)" }}>العملات والسياسات وأسعار الصرف وشروط السداد — بدون خلط مع دفتر الأستاذ.</p></div>
    {error ? <div role="alert" style={{ color: "var(--color-status-danger)" }}>{error}</div> : null}
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(260px,1fr))", gap: "1rem" }}>
      <article style={card}><h2 style={heading}>مرجع العملات</h2><p>البيانات المضبوطة: {currencies.length} عملة نشطة.</p><div style={{ display: "flex", gap: ".4rem", flexWrap: "wrap" }}>{currencies.map(currency => <span key={currency.code} style={chip}>{currency.code} · {currency.nameAr}</span>)}</div></article>
      <article style={card}><h2 style={heading}>معاينة جدول السداد</h2><MoneyField label="إجمالي المستند" value={money} onChange={setMoney} /><p>المعروض: <MoneyDisplay value={money} displayCurrencyCode /></p><button type="button" onClick={() => void preview()} style={button}>حساب 33.33% ثم الرصيد</button>{schedule.length > 0 ? <table style={{ width: "100%", marginTop: ".75rem" }}><tbody>{schedule.map(line => <tr key={line.sequence}><td>الدفعة {line.sequence}</td><td dir="ltr" style={{ textAlign: "end" }}>{line.amount}</td><td>{line.dueDate}</td></tr>)}</tbody></table> : null}</article>
    </div>
  </section>;
}
const card: React.CSSProperties = { background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "1rem" };
const heading: React.CSSProperties = { margin: "0 0 .5rem" };
const chip: React.CSSProperties = { border: "1px solid var(--color-border-default)", borderRadius: "999px", padding: ".25rem .5rem", fontSize: ".8rem" };
const button: React.CSSProperties = { border: 0, borderRadius: "var(--radius-sm)", padding: ".6rem .8rem", background: "var(--color-brand-primary)", color: "white", cursor: "pointer" };
