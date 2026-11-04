import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { usePayrollReport } from "../../hooks/use-payroll-queries.ts";
import { payrollApi } from "../../services/payroll-api.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { formatMoneyAmount, formatRunType } from "../../utils/payroll-formatters.ts";
import type { PayrollReportItem } from "../../types.ts";

export interface PayrollReportsTabProps {
  readonly legalEntityId: string;
}

export function PayrollReportsTab({
  legalEntityId
}: PayrollReportsTabProps): React.JSX.Element {
  const { t, i18n } = useTranslation("payroll");
  const isAr = i18n.language.startsWith("ar");
  const [runTypeFilter, setRunTypeFilter] = useState<string>("all");
  const [searchTerm, setSearchTerm] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [isExporting, setIsExporting] = useState(false);

  const reportParams: { from?: string; to?: string; runType?: string; search?: string; page?: number; pageSize?: number } = {
    page,
    pageSize
  };
  if (runTypeFilter !== "all") {
    reportParams.runType = runTypeFilter;
  }
  if (searchTerm.trim()) {
    reportParams.search = searchTerm.trim();
  }

  const { data: reportData, isLoading } = usePayrollReport(legalEntityId, reportParams);

  const items = reportData?.items ?? [];

  const totalGross = reportData?.totalGross ?? items.reduce((acc: number, r: PayrollReportItem) => acc + r.gross, 0);
  const totalNet = reportData?.totalNet ?? items.reduce((acc: number, r: PayrollReportItem) => acc + r.net, 0);
  const totalDeductions = reportData?.totalDeductions ?? items.reduce((acc: number, r: PayrollReportItem) => acc + r.deductions, 0);
  const totalRuns = reportData?.totalCount ?? items.length;
  const totalPages = Math.ceil(totalRuns / pageSize) || 1;

  const handleExportCsv = async () => {
    setIsExporting(true);
    try {
      const fullReport = await payrollApi.exportReport(legalEntityId, {
        runType: runTypeFilter !== "all" ? runTypeFilter : undefined,
        search: searchTerm.trim() || undefined
      });
      const exportItems = fullReport ?? [];
      const headers = [
        "RunNumber",
        "RunType",
        "PeriodEnd",
        "Employees",
        "Gross",
        "Deductions",
        "NetPay",
        "FinalizationStatus",
        "PaymentState",
        "AccountingState",
        "JournalEntryNumber",
        "PostedToLedger"
      ];
      const rows = exportItems.map((r: PayrollReportItem) => [
        `"${r.runNumber}"`,
        r.runType,
        r.periodEnd ?? "",
        r.employeeCount,
        r.gross,
        r.deductions,
        r.net,
        r.finalizationState,
        r.paymentState,
        r.accountingState ?? "",
        `"${r.journalEntryNumber ?? ""}"`,
        r.postedToLedger ? "Posted" : "Unposted"
      ]);
      const csvContent =
        "data:text/csv;charset=utf-8,\uFEFF" +
        [headers.join(","), ...rows.map((e) => e.join(","))].join("\n");
      const encodedUri = encodeURI(csvContent);
      const link = document.createElement("a");
      link.setAttribute("href", encodedUri);
      link.setAttribute("download", `payroll-register-${legalEntityId}-${new Date().toISOString().slice(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } finally {
      setIsExporting(false);
    }
  };

  const runTypeOptions = [
    { id: "all", label: t("common.allRunTypes") },
    { id: "Regular", label: t("runs.types.regular") },
    { id: "OffCycle", label: t("runs.types.offCycle") },
    { id: "BonusOnly", label: t("runs.types.bonusOnly") },
    { id: "Retro", label: t("runs.types.retro") },
    { id: "FinalSettlement", label: t("runs.types.finalSettlement") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Header Card */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1.25rem 1.5rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("reports.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("reports.tabSubtitle")}
          </p>
        </div>

        <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: "0.25rem" }}>
          <Button
            variant="secondary"
            onPress={handleExportCsv}
            isDisabled={isExporting || items.length === 0}
            style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            {isExporting ? t("common.exporting", "جارٍ التصدير...") : t("reports.exportCsv")}
          </Button>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {t("reports.exportNote", { count: items.length })}
          </span>
        </div>
      </div>

      {/* Aggregate Financial Metrics */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <div
          style={{
            flex: "1 1 200px",
            minWidth: "220px",
            boxSizing: "border-box",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            background: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderTop: "3px solid var(--color-brand-primary)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalNetPayroll")}
          </span>
          <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "var(--color-brand-primary)", marginTop: "0.375rem", wordBreak: "break-word" }}>
            <bdi>{formatMoneyAmount(totalNet, "EGP")}</bdi>
          </div>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem", display: "block" }}>
            {t("reports.disbursedNetDesc")}
          </span>
        </div>

        <div
          style={{
            flex: "1 1 200px",
            minWidth: "220px",
            boxSizing: "border-box",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            background: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderTop: "3px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalGrossPayroll")}
          </span>
          <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "var(--color-content-primary)", marginTop: "0.375rem", wordBreak: "break-word" }}>
            <bdi>{formatMoneyAmount(totalGross, "EGP")}</bdi>
          </div>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem", display: "block" }}>
            {t("reports.totalGrossDesc")}
          </span>
        </div>

        <div
          style={{
            flex: "1 1 200px",
            minWidth: "220px",
            boxSizing: "border-box",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            background: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderTop: "3px solid var(--color-status-warning)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalDeductions")}
          </span>
          <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "var(--color-status-warning)", marginTop: "0.375rem", wordBreak: "break-word" }}>
            <bdi>{formatMoneyAmount(totalDeductions > 0 ? -totalDeductions : totalDeductions, "EGP")}</bdi>
          </div>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem", display: "block" }}>
            {t("reports.totalDeductionsDesc")}
          </span>
        </div>

        <div
          style={{
            flex: "1 1 200px",
            minWidth: "220px",
            boxSizing: "border-box",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            background: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderTop: "3px solid var(--color-status-success)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalRunsExecuted")}
          </span>
          <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "var(--color-content-primary)", marginTop: "0.375rem" }}>
            {totalRuns}
          </div>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem", display: "block" }}>
            {t("reports.totalRunsDesc")}
          </span>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          flexWrap: "wrap",
          padding: "1rem 1.25rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-md)",
          border: "1px solid var(--color-border-default)"
        }}
      >
        <div style={{ minWidth: "200px" }}>
          <Select
            label={t("reports.filterByRunType")}
            selectedKey={runTypeFilter}
            onSelectionChange={(key) => {
              setRunTypeFilter(String(key));
              setPage(1);
            }}
            options={runTypeOptions}
          />
        </div>

        <div style={{ flex: 1, minWidth: "240px" }}>
          <TextField
            label={t("common.search")}
            value={searchTerm}
            onChange={(val) => {
              setSearchTerm(val);
              setPage(1);
            }}
            placeholder={t("reports.searchPlaceholder", "بحث برقم المسير...")}
          />
        </div>
      </div>

      {/* Reports Table Surface */}
      <div
        style={{
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)",
          overflow: "hidden"
        }}
      >
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid var(--color-border-subtle)", background: "var(--color-surface-subtle)" }}>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.runNumber")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.runType")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.period")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.employees")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.gross")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.net")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.status")}
                </th>
                <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("reports.glIntegration")}
                </th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={8} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ padding: "3rem 2rem", textAlign: "center" }}>
                    <p style={{ margin: 0, fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("reports.emptyTitle")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("reports.emptyDesc")}
                    </p>
                  </td>
                </tr>
              ) : (
                items.map((r: PayrollReportItem) => (
                  <tr
                    key={r.runId}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {r.runNumber}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span style={{ padding: "0.2rem 0.5rem", borderRadius: "var(--radius-sm)", background: "var(--color-surface-subtle)", fontSize: "0.75rem", fontWeight: 600 }}>
                        {formatRunType(r.runType, isAr)}
                      </span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-secondary)" }}>
                      {r.periodEnd ?? "—"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      {r.employeeCount}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <bdi>{formatMoneyAmount(r.gross, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                      <bdi>{formatMoneyAmount(r.net, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={r.finalizationState} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {r.postedToLedger && r.accountingState === "Posted" ? (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "0.375rem",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            padding: "0.2rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            background: "var(--color-status-success-subtle)",
                            color: "var(--color-status-success)"
                          }}
                        >
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3">
                            <polyline points="20 6 9 17 4 12" />
                          </svg>
                          {r.journalEntryNumber ? `${t("reports.postedToLedger")}: ${r.journalEntryNumber}` : t("reports.postedToLedger")}
                        </span>
                      ) : r.accountingState === "Ready" ? (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "0.375rem",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            padding: "0.2rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            background: "var(--color-status-warning-subtle, #fef3c7)",
                            color: "var(--color-status-warning, #92400e)"
                          }}
                        >
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <circle cx="12" cy="12" r="10" />
                            <polyline points="12 6 12 12 16 14" />
                          </svg>
                          {t("reports.accountingReady", "قيد الترحيل / بانتظار المطابقة")}
                        </span>
                      ) : r.accountingState === "Failed" ? (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "0.375rem",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            padding: "0.2rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            background: "var(--color-status-danger-subtle, #fee2e2)",
                            color: "var(--color-status-danger, #b91c1c)"
                          }}
                        >
                          {t("reports.accountingFailed", "فشل الترحيل المحاسبي")}
                        </span>
                      ) : (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "0.375rem",
                            fontSize: "0.75rem",
                            fontWeight: 500,
                            padding: "0.2rem 0.5rem",
                            borderRadius: "var(--radius-sm)",
                            background: "var(--color-surface-subtle)",
                            color: "var(--color-content-muted)"
                          }}
                        >
                          {t("reports.unpostedToLedger", "غير مرحل (بانتظار الإقفال)")}
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Server-Side Pagination Bar */}
        <div
          style={{
            padding: "0.75rem 1.25rem",
            borderTop: "1px solid var(--color-border-default)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("reports.totalRecords", { count: totalRuns, defaultValue: `إجمالي السجلات: ${totalRuns}` })}
          </span>
          <Pagination
            page={page}
            totalPages={totalPages}
            totalItems={totalRuns}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
          />
        </div>
      </div>
    </div>
  );
}
