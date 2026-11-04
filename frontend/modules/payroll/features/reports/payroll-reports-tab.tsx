import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { usePayrollReport } from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import type { PayrollReportItem } from "../../types.ts";

export interface PayrollReportsTabProps {
  readonly legalEntityId: string;
}

export function PayrollReportsTab({
  legalEntityId
}: PayrollReportsTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const [runTypeFilter, setRunTypeFilter] = useState<string>("all");
  const [searchTerm, setSearchTerm] = useState("");

  const reportParams: { from?: string; to?: string; runType?: string; page?: number; pageSize?: number } = {
    pageSize: 100
  };
  if (runTypeFilter !== "all") {
    reportParams.runType = runTypeFilter;
  }

  const { data: reportData, isLoading } = usePayrollReport(legalEntityId, reportParams);

  const items = reportData?.items ?? [];
  const filtered = items.filter((r: PayrollReportItem) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      r.runNumber.toLowerCase().includes(term) ||
      r.runType.toLowerCase().includes(term)
    );
  });

  const totalGross = reportData?.totalGross ?? filtered.reduce((acc: number, r: PayrollReportItem) => acc + r.gross, 0);
  const totalNet = reportData?.totalNet ?? filtered.reduce((acc: number, r: PayrollReportItem) => acc + r.net, 0);
  const totalDeductions = reportData?.totalDeductions ?? filtered.reduce((acc: number, r: PayrollReportItem) => acc + r.deductions, 0);
  const totalEmployerCost = totalGross;

  const handleExportCsv = () => {
    const headers = [
      "RunNumber",
      "RunType",
      "PeriodEnd",
      "Employees",
      "Gross",
      "Deductions",
      "NetPay",
      "Status",
      "PaymentState"
    ];
    const rows = filtered.map((r: PayrollReportItem) => [
      `"${r.runNumber}"`,
      r.runType,
      r.periodEnd ?? "",
      r.employeeCount,
      r.gross,
      r.deductions,
      r.net,
      r.finalizationState,
      r.paymentState
    ]);
    const csvContent = "data:text/csv;charset=utf-8," + [headers.join(","), ...rows.map((e) => e.join(","))].join("\n");
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `payroll-register-${legalEntityId}-${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
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

        <Button
          variant="secondary"
          onPress={handleExportCsv}
          isDisabled={filtered.length === 0}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="7 10 12 15 17 10" />
            <line x1="12" y1="15" x2="12" y2="3" />
          </svg>
          {t("reports.exportCsvAction")}
        </Button>
      </div>

      {/* Aggregate KPI Summary Cards */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "1rem"
        }}
      >
        <div
          style={{
            padding: "1.25rem",
            background: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalGrossPayroll")}
          </span>
          <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)", marginTop: "0.25rem" }}>
            <bdi>{totalGross.toLocaleString()} EGP</bdi>
          </div>
        </div>

        <div
          style={{
            padding: "1.25rem",
            background: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalNetPayroll")}
          </span>
          <div style={{ fontSize: "1.25rem", fontWeight: 800, color: "var(--color-status-success)", marginTop: "0.25rem" }}>
            <bdi>{totalNet.toLocaleString()} EGP</bdi>
          </div>
        </div>

        <div
          style={{
            padding: "1.25rem",
            background: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalDeductions")}
          </span>
          <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-danger)", marginTop: "0.25rem" }}>
            <bdi>-{totalDeductions.toLocaleString()} EGP</bdi>
          </div>
        </div>

        <div
          style={{
            padding: "1.25rem",
            background: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "var(--shadow-sm)"
          }}
        >
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
            {t("reports.totalEmployerCost")}
          </span>
          <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-secondary)", marginTop: "0.25rem" }}>
            <bdi>{totalEmployerCost.toLocaleString()} EGP</bdi>
          </div>
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
        <div style={{ flex: 1, minWidth: "240px" }}>
          <TextField
            label={t("common.search")}
            value={searchTerm}
            onChange={setSearchTerm}
            placeholder={t("reports.searchPlaceholder")}
          />
        </div>

        <div style={{ width: "200px" }}>
          <Select
            label={t("common.runType")}
            selectedKey={runTypeFilter}
            onSelectionChange={(val) => {
              if (val) setRunTypeFilter(String(val));
            }}
            options={runTypeOptions}
          />
        </div>
      </div>

      {/* Register Table */}
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
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr
                style={{
                  background: "var(--color-surface-subtle)",
                  borderBottom: "1px solid var(--color-border-default)",
                  textAlign: "start"
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.period")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.runType")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.payDate")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.employees")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.gross")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.net")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.status")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.ledgerStatus")}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={8} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
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
                filtered.map((r: PayrollReportItem) => (
                  <tr
                    key={r.runId}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      {r.runNumber}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span
                        style={{
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          padding: "0.2rem 0.5rem",
                          borderRadius: "var(--radius-sm)",
                          background: "var(--color-surface-subtle)",
                          color: "var(--color-content-primary)"
                        }}
                      >
                        {r.runType}
                      </span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                      {r.periodEnd ?? "—"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      {r.employeeCount}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <bdi>{r.gross.toLocaleString()} EGP</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                      <bdi>{r.net.toLocaleString()} EGP</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={r.finalizationState} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
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
                        {t("reports.postedToLedger")}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
