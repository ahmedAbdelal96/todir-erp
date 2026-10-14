import { useState } from "react";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation("navigation");
  const { density } = useOutletContext<WorkspaceOutletContext>();
  const [amount, setAmount] = useState({ amount: "12500.50", currency: "SAR" });
  const [selectedCity, setSelectedCity] = useState<string | null>("ruh");
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const sampleColumns: DataGridColumn<PreviewRow>[] = [
    { id: "code", header: t("previewPage.accountCode"), width: 120 },
    { id: "name", header: t("previewPage.accountName"), width: "1fr" },
    { id: "balance", header: t("previewPage.currentBalance"), width: 160, align: "end" }
  ];
  const sampleRows: readonly PreviewRow[] = [
    { id: "1", code: "1010", name: "الصندوق الرئيسي", balance: "25,400.00 ر.س" },
    { id: "2", code: "1020", name: "بنك الراجحي - جاري", balance: "148,920.50 ر.س" },
    { id: "3", code: "1030", name: "البنك الأهلي التجاري", balance: "82,150.00 ر.س" },
    { id: "4", code: "1210", name: "ذمم العملاء المدينة", balance: "63,200.00 ر.س" }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <PageHeader title={t("previewPage.title")} subtitle={t("previewPage.subtitle")} />
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem" }}>
        <div style={surfaceStyle}>
          <h3 style={headingStyle}>{t("previewPage.moneyFieldTitle")}</h3>
          <MoneyField label={t("previewPage.amountDue")} value={amount} onChange={setAmount} description={t("previewPage.exactNumbersDescription")} />
          <div style={{ marginTop: "1rem", fontSize: "0.875rem" }}><span>{t("previewPage.recordedValue")}: </span><MoneyDisplay value={amount} displayCurrencyCode /></div>
        </div>
        <div style={surfaceStyle}>
          <h3 style={headingStyle}>{t("previewPage.comboBoxTitle")}</h3>
          <ComboBox label={t("previewPage.cityBranch")} placeholder={t("previewPage.chooseCity")} selectedKey={selectedCity} onSelectionChange={(key) => setSelectedCity(key as string)} options={[{ id: "ruh", label: t("previewPage.riyadhHq") }, { id: "jed", label: t("previewPage.jeddahBranch") }, { id: "dmm", label: t("previewPage.dammamBranch") }]} />
          <div style={{ marginTop: "1rem" }}><Button variant="secondary" onPress={() => setIsDialogOpen(true)}>{t("previewPage.openConfirmation")}</Button></div>
        </div>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
        <h3 style={{ margin: 0, fontSize: "1rem" }}>{t("previewPage.dataGridTitle")}</h3>
        <DataGrid columns={sampleColumns} staticRows={sampleRows} density={density} enableRtl />
      </div>
      <Dialog isOpen={isDialogOpen} onOpenChange={setIsDialogOpen} title={t("previewPage.confirmFinanceTitle")} description={t("previewPage.confirmFinanceDescription")} size="medium">
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.5rem" }}>
          <Button variant="ghost" onPress={() => setIsDialogOpen(false)}>{t("previewPage.cancel")}</Button>
          <Button variant="primary" onPress={() => setIsDialogOpen(false)}>{t("previewPage.confirmPosting")}</Button>
        </div>
      </Dialog>
    </div>
  );
}

const surfaceStyle: React.CSSProperties = { backgroundColor: "var(--color-surface-card)", padding: "1.25rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" };
const headingStyle: React.CSSProperties = { margin: "0 0 1rem 0", fontSize: "1rem" };
