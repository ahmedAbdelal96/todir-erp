import React from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useAssetBooksQuery,
  useDepreciationRunsQuery,
  usePostDepreciationRunMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { DepreciationRunDto } from "../../types.ts";
import {
  CalculatorIcon,
  BookOpenIcon,
  PlusIcon,
  CheckCircle2Icon,
  RotateCcwIcon
} from "../../components/fixed-assets-icons.tsx";

export interface DepreciationTabProps {
  readonly legalEntityId: string;
  readonly onOpenPrepareRunModal: () => void;
  readonly onOpenReverseRunModal: (run: DepreciationRunDto) => void;
  readonly onOpenNewBookModal: () => void;
}

export function DepreciationTab({
  legalEntityId,
  onOpenPrepareRunModal,
  onOpenReverseRunModal,
  onOpenNewBookModal
}: DepreciationTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  const booksQuery = useAssetBooksQuery(legalEntityId);
  const runsQuery = useDepreciationRunsQuery(legalEntityId);
  const postMutation = usePostDepreciationRunMutation(legalEntityId);

  const books = booksQuery.data ?? [];
  const runs = runsQuery.data?.items ?? [];

  const handlePostRun = async (runId: string) => {
    try {
      await postMutation.mutateAsync({ runId });
    } catch {
      // handled by mutation
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }} data-testid="depreciation-tab">
      {/* Books Summary Cards */}
      <div
        style={{
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <BookOpenIcon size={20} color="#102a43" />
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("books.heading", "دفاتر الأصول وقواعد الإهلاك")}
            </h3>
          </div>

          <Button variant="secondary" size="compact" onClick={onOpenNewBookModal} data-testid="new-book-btn">
            <PlusIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
            {t("books.newBook", "إضافة دفتر")}
          </Button>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: "1rem" }}>
          {books.map((b) => (
            <div
              key={b.id}
              style={{
                padding: "1rem",
                backgroundColor: "#f8fafc",
                border: "1px solid #cbd5e1",
                borderRadius: "0.375rem"
              }}
              data-testid={`book-card-${b.code}`}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ fontWeight: 700, fontSize: "0.9375rem", color: "#0f172a" }}>{b.name}</span>
                <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "#1e3a8a", backgroundColor: "#dbeafe", padding: "0.15rem 0.5rem", borderRadius: "9999px" }}>
                  {b.code}
                </span>
              </div>
              <div style={{ fontSize: "0.8125rem", color: "#64748b", marginTop: "0.5rem" }}>
                {t("fields.bookType", "النوع")}: {b.postsToLedger ? "Corporate" : "Tax"}
              </div>
              <div style={{ fontSize: "0.75rem", color: b.postsToLedger ? "#16a34a" : "#64748b", marginTop: "0.25rem", fontWeight: 500 }}>
                {b.postsToLedger ? t("books.postsToLedgerTrue", "✓ يرحل آلياً إلى دفتر الأستاذ (GL)") : t("books.postsToLedgerFalse", "دفتر موازي لا يرحل للأستاذ")}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Depreciation Runs Table */}
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
            alignItems: "center"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <CalculatorIcon size={20} color="#102a43" />
            <div>
              <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
                {t("depreciation.runsHeading", "مسيرات الإهلاك الشهري (Depreciation Runs)")}
              </h3>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
                {t("depreciation.runsDesc", "احتساب واعتماد وترحيل إهلاك الأصول الثابتة وإقفال القيود الشهرية.")}
              </p>
            </div>
          </div>

          <Button variant="primary" onClick={onOpenPrepareRunModal} data-testid="prepare-run-btn">
            <PlusIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {t("depreciation.prepareNewRun", "تجهيز مسير شهري جديد")}
          </Button>
        </div>

        <Table isBordered={false} data-testid="depreciation-runs-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.period", "الفترة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.cutoffDate", "تاريخ الإقفال")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("depreciation.assetsCount", "عدد الأصول")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("depreciation.totalDeprAmount", "إجمالي قسط الإهلاك")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.status", "الحالة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
            </tr>
          </thead>
          <tbody>
            {runs.length === 0 ? (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                  {t("depreciation.noRuns", "لا توجد مسيرات إهلاك سابقة")}
                </td>
              </tr>
            ) : (
              runs.map((row) => (
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>
                      {row.periodYear} / {row.periodMonth}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.cutoffDate}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.totalAssetsCount}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 700, color: "#dc2626" }}>
                      {(row.totalDepreciationAmount ?? 0).toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {row.currency ?? "EGP"}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "0.2rem 0.5rem",
                        borderRadius: "9999px",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: row.status === "Posted" ? "#f0fdf4" : "#eff6ff",
                        color: row.status === "Posted" ? "#16a34a" : "#1e40af"
                      }}
                    >
                      {row.status === "Posted"
                        ? t("depreciation.statusPosted", "مرحل للأستاذ العام (Posted)")
                        : t("depreciation.statusCalculated", "تم الاحتساب")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.5rem" }}>
                      {row.status === "Calculated" && (
                        <Button
                          variant="primary"
                          size="compact"
                          onClick={() => handlePostRun(row.id)}
                          isLoading={postMutation.isPending}
                          data-testid={`post-run-btn-${row.id}`}
                        >
                          <CheckCircle2Icon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                          {t("depreciation.postToGl", "ترحيل لدفتر الأستاذ")}
                        </Button>
                      )}
                      {row.status === "Posted" && (
                        <Button
                          variant="secondary"
                          size="compact"
                          onClick={() => onOpenReverseRunModal(row)}
                          data-testid={`reverse-run-btn-${row.id}`}
                        >
                          <RotateCcwIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                          {t("depreciation.reverseRun", "عكس المسير")}
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </Table>
      </div>
    </div>
  );
}
