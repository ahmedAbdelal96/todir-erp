import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { MoneyField } from "../components/fields/money-field.tsx";
import { MoneyDisplay } from "../components/fields/money-display.tsx";
import { DataGrid, type DataGridColumn } from "../components/grid/index.ts";
import { ComboBox } from "../components/primitives/combobox.tsx";
import { Button } from "../components/primitives/button.tsx";
import { Dialog } from "../components/primitives/dialog.tsx";
import { PageHeader } from "../components/pages/page-header.tsx";
import type { WorkspaceOutletContext } from "./router/route-contract.ts";

type PreviewRow = { id: string; code: string; name: string; balance: string };

export function PreviewPage(): React.JSX.Element {
  const { density } = useOutletContext<WorkspaceOutletContext>();
  const [amount, setAmount] = useState({ amount: "12500.50", currency: "SAR" });
  const [selectedCity, setSelectedCity] = useState<string | null>("ruh");
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const sampleColumns: DataGridColumn<PreviewRow>[] = [
    { id: "code", header: "كود الحساب", width: 120 },
    { id: "name", header: "اسم الحساب", width: "1fr" },
    { id: "balance", header: "الرصيد الحالي", width: 160, align: "end" }
  ];
  const sampleRows: readonly PreviewRow[] = [
    { id: "1", code: "1010", name: "الصندوق الرئيسي", balance: "25,400.00 ر.س" },
    { id: "2", code: "1020", name: "بنك الراجحي - جاري", balance: "148,920.50 ر.س" },
    { id: "3", code: "1030", name: "البنك الأهلي التجاري", balance: "82,150.00 ر.س" },
    { id: "4", code: "1210", name: "ذمم العملاء المدينة", balance: "63,200.00 ر.س" }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <PageHeader title="معاينة عناصر التصميم ومكونات المنظومة" subtitle="فحص مباشر للمكونات التفاعلية والحقول والجداول" />
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem" }}>
        <div style={surfaceStyle}>
          <h3 style={headingStyle}>حقل الإدخال النقدي الدقيق (MoneyField)</h3>
          <MoneyField label="المبلغ المالي المستحق" value={amount} onChange={setAmount} description="يدعم الأرقام الدقيقة بدون كسور عائمة" />
          <div style={{ marginTop: "1rem", fontSize: "0.875rem" }}><span>القيمة المسجلة: </span><MoneyDisplay value={amount} displayCurrencyCode /></div>
        </div>
        <div style={surfaceStyle}>
          <h3 style={headingStyle}>قائمة الاختيار والبحث (ComboBox)</h3>
          <ComboBox label="المدينة / الفرع" placeholder="اختر المدينة..." selectedKey={selectedCity} onSelectionChange={(key) => setSelectedCity(key as string)} options={[{ id: "ruh", label: "الرياض (المقر الرئيسي)" }, { id: "jed", label: "جدة (فرع المنطقة الغربية)" }, { id: "dmm", label: "الدمام (فرع المنطقة الشرقية)" }]} />
          <div style={{ marginTop: "1rem" }}><Button variant="secondary" onPress={() => setIsDialogOpen(true)}>فتح نافذة تأكيد تجريبية</Button></div>
        </div>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
        <h3 style={{ margin: 0, fontSize: "1rem" }}>جدول البيانات عالي الكفاءة (DataGrid)</h3>
        <DataGrid columns={sampleColumns} staticRows={sampleRows} density={density} enableRtl />
      </div>
      <Dialog isOpen={isDialogOpen} onOpenChange={setIsDialogOpen} title="تأكيد العملية المالية" description="هل أنت متأكد من رغبتك في إتمام ترحيل القيد المحاسبي؟" size="medium">
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.5rem" }}>
          <Button variant="ghost" onPress={() => setIsDialogOpen(false)}>إلغاء</Button>
          <Button variant="primary" onPress={() => setIsDialogOpen(false)}>تأكيد الترحيل</Button>
        </div>
      </Dialog>
    </div>
  );
}

const surfaceStyle: React.CSSProperties = { backgroundColor: "var(--color-surface-card)", padding: "1.25rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" };
const headingStyle: React.CSSProperties = { margin: "0 0 1rem 0", fontSize: "1rem" };
