import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import {
  useBookGlReconciliationQuery,
  useAssetRegisterReportQuery
} from "../../hooks/use-fixed-assets-queries.ts";
import { fixedAssetsApi } from "../../services/fixed-assets-api.ts";
import {
  CheckCircle2Icon,
  DownloadIcon,
  RefreshCwIcon,
  FileSpreadsheetIcon
} from "../../components/fixed-assets-icons.tsx";

export interface ReportsTabProps {
  readonly legalEntityId: string;
}

export function ReportsTab({ legalEntityId }: ReportsTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const isMobile = useIsMobile(768);

  const [asOfDate, setAsOfDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const reconQuery = useBookGlReconciliationQuery(legalEntityId);
  const reportQuery = useAssetRegisterReportQuery({
    legalEntityId,
    asOfDate
  });

  const recon = reconQuery.data;
  const reportItems = reportQuery.data?.items ?? [];

  // Currency from authoritative reconciliation context or default empty
  const currency = recon?.currencyCode ?? "";

  // Authoritative totals from reconciliation or report items
  const displayTotalCost = recon ? (recon.subledgerAssetCost ?? 0) : reportItems.reduce((acc, curr) => acc + curr.grossCost, 0);
  const displayTotalAccDep = recon ? (recon.subledgerAccDep ?? 0) : reportItems.reduce((acc, curr) => acc + curr.accumulatedDepreciation, 0);
  // Authoritative NBV accounts for impairment and revaluation effects
  const displayTotalNbv = recon
    ? (recon.assetNetBookValue ?? (displayTotalCost - displayTotalAccDep))
    : reportItems.reduce((acc, curr) => acc + curr.netBookValue, 0);

  // Authoritative Backend CSV Export
  const handleExportCsv = async () => {
    try {
      setExporting(true);
      setExportError(null);
      const blob = await fixedAssetsApi.exportAssetRegister(legalEntityId, asOfDate);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `fixed-asset-register-${asOfDate}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err: unknown) {
      setExportError(err instanceof Error ? err.message : t("reports.exportFailed", "فشل تصدير التقرير"));
    } finally {
      setExporting(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }} data-testid="reports-tab">
      {exportError && (
        <div
          role="alert"
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "#fef2f2",
            border: "1px solid #f87171",
            borderRadius: "0.375rem",
            color: "#991b1b",
            fontSize: "0.875rem"
          }}
        >
          {exportError}
        </div>
      )}

      {/* Book-to-GL Reconciliation Card */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem"
        }}
        data-testid="reconciliation-card"
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.5rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <CheckCircle2Icon size={22} color="#16a34a" />
            <div>
              <h3 style={{ margin: 0, fontSize: "1.0625rem", fontWeight: 700, color: "#102a43" }}>
                {t("reports.reconHeading", "مطابقة سجل الأصول مع دفتر الأستاذ العام (Subledger to GL Reconciliation)")}
              </h3>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
                {t("reports.reconDesc", "التحقق المالي من تطابق أرصدة الأصول ومجمع الإهلاك بين دفتر الأصول الفرعي وميزان المراجعة.")}
              </p>
            </div>
          </div>

          <Button variant="secondary" size="compact" onClick={() => reconQuery.refetch()} data-testid="refresh-recon-btn">
            <RefreshCwIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
            {t("common.refresh", "تحديث المطابقة")}
          </Button>
        </div>

        {reconQuery.isLoading ? (
          <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading", "جاري التحقق من المطابقة...")}
          </div>
        ) : recon ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : "1fr 1fr 1fr", gap: "1rem" }}>
              <div
                style={{
                  padding: "1rem",
                  backgroundColor: "#f8fafc",
                  border: "1px solid #cbd5e1",
                  borderRadius: "0.375rem"
                }}
              >
                <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>{t("reports.assetCostMatch", "تكلفة الأصول (ح/ 150000)")}</div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.5rem", fontSize: "0.875rem" }}>
                  <span>{t("reports.subledger", "دفتر الأصول")}:</span>
                  <strong style={{ color: "#0f172a" }}>
                    {recon.subledgerAssetCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
                  </strong>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.25rem", fontSize: "0.875rem" }}>
                  <span>{t("reports.generalLedger", "الأستاذ العام")}:</span>
                  <strong style={{ color: "#0f172a" }}>
                    {recon.glAssetCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
                  </strong>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.5rem", borderTop: "1px solid #e2e8f0", paddingTop: "0.5rem", fontSize: "0.8125rem" }}>
                  <span>{t("reports.variance", "الفارق (Variance)")}:</span>
                  <strong style={{ color: recon.costVariance === 0 ? "#16a34a" : "#dc2626" }}>
                    {recon.costVariance === 0 ? t("reports.fullyMatched", "0.00 (مطابق تماماً ✓)") : `${recon.costVariance.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} ${currency}`}
                  </strong>
                </div>
              </div>

              <div
                style={{
                  padding: "1rem",
                  backgroundColor: "#f8fafc",
                  border: "1px solid #cbd5e1",
                  borderRadius: "0.375rem"
                }}
              >
                <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>{t("reports.accDepMatch", "مجمع الإهلاك (ح/ 159000)")}</div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.5rem", fontSize: "0.875rem" }}>
                  <span>{t("reports.subledger", "دفتر الأصول")}:</span>
                  <strong style={{ color: "#0f172a" }}>
                    {recon.subledgerAccDep.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
                  </strong>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.25rem", fontSize: "0.875rem" }}>
                  <span>{t("reports.generalLedger", "الأستاذ العام")}:</span>
                  <strong style={{ color: "#0f172a" }}>
                    {recon.glAccDep.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
                  </strong>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.5rem", borderTop: "1px solid #e2e8f0", paddingTop: "0.5rem", fontSize: "0.8125rem" }}>
                  <span>{t("reports.variance", "الفارق (Variance)")}:</span>
                  <strong style={{ color: recon.accDepVariance === 0 ? "#16a34a" : "#dc2626" }}>
                    {recon.accDepVariance === 0 ? t("reports.fullyMatched", "0.00 (مطابق تماماً ✓)") : `${recon.accDepVariance.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} ${currency}`}
                  </strong>
                </div>
              </div>

              <div
                style={{
                  padding: "1rem",
                  backgroundColor:
                    recon.status === "Healthy"
                      ? "#f0fdf4"
                      : recon.status === "NotApplicable"
                      ? "#f8fafc"
                      : "#fef2f2",
                  border: `1px solid ${
                    recon.status === "Healthy"
                      ? "#86efac"
                      : recon.status === "NotApplicable"
                      ? "#cbd5e1"
                      : "#fca5a5"
                  }`,
                  borderRadius: "0.375rem",
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "center",
                  textAlign: "center"
                }}
              >
                <div
                  style={{
                    fontSize: "0.875rem",
                    fontWeight: 700,
                    color:
                      recon.status === "Healthy"
                        ? "#15803d"
                        : recon.status === "NotApplicable"
                        ? "#475569"
                        : "#991b1b"
                  }}
                >
                  {recon.status === "Healthy"
                    ? t("reports.reconciledSuccess", "مطابق ومستقر (Healthy) — الحسابات متطابقة بنسبة 100% ولا توجد فروق")
                    : recon.status === "NotApplicable"
                    ? t("reports.statusNotApplicable", "دفتر غير مالي / لا يرحل إلى الأستاذ العام (Not Applicable)")
                    : t("reports.discrepancyAlert", "يوجد تباين مالي يستوجب التحقق (Difference)")}
                </div>
                <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.5rem" }}>
                  {t("reports.reconciliationAsOf", "آخر فحص ومطابقة في")}: {new Date().toLocaleTimeString("ar-EG")}
                  {currency && <span> ({currency})</span>}
                </div>
              </div>
            </div>

            {/* Explanatory Notice when Difference Exists */}
            {recon.status === "Difference" && (
              <div
                style={{
                  padding: "0.75rem 1rem",
                  backgroundColor: "#fffbeb",
                  border: "1px solid #fcd34d",
                  borderRadius: "0.375rem",
                  color: "#92400e",
                  fontSize: "0.8125rem",
                  lineHeight: 1.5
                }}
                data-testid="recon-difference-explanation"
              >
                {t("reports.differenceReasonNotice", "ملاحظة تدقيق مالي: يوجد تباين بين رصيد دفتر الأصول الثابتة الفرعي وحسابات الأستاذ العام الرقابية. يرجى مراجعة الحركات المالية غير المرحلة أو التسويات المعلقة عبر شاشات التدقيق والتفاصيل.")}
              </div>
            )}
          </div>
        ) : null}
      </div>

      {/* As-Of-Date Fixed Asset Register Report */}
      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <div
          style={{
            padding: "1rem",
            borderBottom: "1px solid #e2e8f0",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <FileSpreadsheetIcon size={20} color="#102a43" />
            <div>
              <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
                {t("reports.registerReportHeading", "تقرير سجل الأصول الثابتة المالي (Asset Register Report)")}
              </h3>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
                {t("reports.registerReportDesc", "تقرير تفصيلي بموقف الأصول وإهلاكاتها وصافي قيمتها الدفترية في تاريخ محدد.")}
              </p>
            </div>
          </div>

          <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ fontSize: "0.8125rem", color: "#334155", fontWeight: 500 }}>
                {t("reports.asOfDate", "حتى تاريخ:")}
              </span>
              <input
                type="date"
                value={asOfDate}
                onChange={(e) => setAsOfDate(e.target.value)}
                style={{
                  padding: "0.375rem 0.5rem",
                  fontSize: "0.8125rem",
                  borderRadius: "0.375rem",
                  border: "1px solid #cbd5e1"
                }}
                data-testid="as-of-date-input"
              />
            </div>

            <Button
              variant="secondary"
              size="compact"
              onClick={handleExportCsv}
              isDisabled={exporting}
              data-testid="export-report-csv-btn"
            >
              <DownloadIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
              {exporting ? t("common.exporting", "جاري التصدير...") : t("register.exportCsv", "تصدير CSV")}
            </Button>
          </div>
        </div>

        {/* Summary Metric Ribbon */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: isMobile ? "1fr 1fr" : "1fr 1fr 1fr 1fr",
            gap: "1rem",
            padding: "0.75rem 1rem",
            backgroundColor: "#f8fafc",
            borderBottom: "1px solid #e2e8f0"
          }}
        >
          <div>
            <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("reports.totalAssets", "إجمالي عدد الأصول")}</div>
            <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#0f172a" }}>{reportItems.length}</div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("reports.totalHistoricalCost", "إجمالي التكلفة التاريخية")}</div>
            <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#1e3a8a" }}>
              {displayTotalCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("reports.totalAccumulatedDepreciation", "إجمالي مجمع الإهلاك")}</div>
            <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#dc2626" }}>
              {displayTotalAccDep.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("reports.totalNetBookValue", "إجمالي صافي القيمة الدفترية")}</div>
            <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#16a34a" }}>
              {displayTotalNbv.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}
            </div>
          </div>
        </div>

        <div style={{ width: "100%", overflowX: "auto" }}>
          <Table isBordered={false} data-testid="asset-register-report-table">
            <thead>
              <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
                <th style={{ padding: "0.75rem 1rem", minWidth: "120px" }}>{t("fields.assetCode", "كود الأصل")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "180px" }}>{t("fields.assetName", "اسم الأصل")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "130px" }}>{t("fields.category", "التصنيف")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "130px", textAlign: "end" }}>{t("fields.acquisitionCost", "التكلفة")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "130px", textAlign: "end" }}>{t("fields.accumulatedDepreciation", "مجمع الإهلاك")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "130px", textAlign: "end" }}>{t("fields.netBookValue", "صافي القيمة الدفترية")}</th>
                <th style={{ padding: "0.75rem 1rem", minWidth: "110px" }}>{t("fields.custodian", "العهدة")}</th>
              </tr>
            </thead>
            <tbody>
              {reportItems.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                    {t("reports.noAssetsInReport", "لا توجد أصول في التاريخ المحدد")}
                  </td>
                </tr>
              ) : (
                reportItems.map((row) => (
                  <tr key={row.assetId} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span style={{ fontWeight: 600, color: "#1e3a8a" }}><bdi>{row.assetNumber}</bdi></span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>{row.name}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>{row.assetNature}</td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <span>{row.grossCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}</span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <span style={{ color: "#dc2626" }}>{row.accumulatedDepreciation.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}</span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <span style={{ color: "#16a34a", fontWeight: 700 }}>{row.netBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {currency}</span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>{row.custodianName ?? "—"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </Table>
        </div>
      </div>
    </div>
  );
}
