import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import {
  useSelfPayslips,
  usePayslips
} from "../../hooks/use-payroll-queries.ts";
import { payrollApi } from "../../services/payroll-api.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { PayslipViewModal } from "./payslip-view-modal.tsx";
import { useEmployees } from "../../../hr/public.ts";
import { formatMoneyAmount, resolveEmployeeDisplay } from "../../utils/payroll-formatters.ts";
import type { Payslip } from "../../types.ts";

export interface PayslipsTabProps {
  readonly legalEntityId: string;
}

export function PayslipsTab({
  legalEntityId
}: PayslipsTabProps): React.JSX.Element {
  const { t, i18n } = useTranslation("payroll");
  const isAr = i18n.language.startsWith("ar");
  const [viewMode, setViewMode] = useState<"self" | "company">("company");
  const [searchTerm, setSearchTerm] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [selectedPayslip, setSelectedPayslip] = useState<Payslip | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [isExporting, setIsExporting] = useState(false);

  // Self payslips query
  const selfQuery = useSelfPayslips(legalEntityId, {
    page,
    pageSize
  });

  // Company payslips query
  const companyQuery = usePayslips(legalEntityId, {
    page,
    pageSize,
    search: searchTerm.trim() || undefined
  });

  // Employees for label resolution fallback
  const employeesListQuery = useEmployees(legalEntityId ? { legalEntityId } : undefined);
  const employeesList = employeesListQuery.data?.items ?? [];

  const displayItems: readonly Payslip[] =
    viewMode === "self"
      ? (selfQuery.data ?? [])
      : (companyQuery.data?.items ?? []);

  const totalRecords =
    viewMode === "self"
      ? (selfQuery.data?.length ?? 0)
      : (companyQuery.data?.totalCount ?? 0);

  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));

  const isLoading = viewMode === "self" ? selfQuery.isLoading : companyQuery.isLoading;

  const openPayslip = (item: Payslip) => {
    setSelectedPayslip(item);
    setModalOpen(true);
  };

  const handleExportCsv = async () => {
    setIsExporting(true);
    try {
      const fullList = await payrollApi.exportPayslips(legalEntityId, {
        search: searchTerm.trim() || undefined
      });
      const rowsToExport = fullList ?? [];
      const headers = [
        "PayslipNumber",
        "EmployeeName",
        "EmploymentNumber",
        "EmploymentId",
        "Gross",
        "Deductions",
        "NetPay",
        "EmployerCost",
        "Status",
        "FinalizedAt"
      ];
      const rows = rowsToExport.map((p) => [
        `"${p.payslipNumber}"`,
        `"${(p.employeeName || resolveEmployeeDisplay(p.employmentId, employeesList, isAr)).replace(/"/g, '""')}"`,
        `"${p.employmentNumber ?? ""}"`,
        `"${p.employmentId}"`,
        p.gross,
        p.deductions,
        p.net,
        p.employerCost,
        p.status,
        p.finalizedAt ?? ""
      ]);
      const csvContent =
        "data:text/csv;charset=utf-8,\uFEFF" +
        [headers.join(","), ...rows.map((e) => e.join(","))].join("\n");
      const encodedUri = encodeURI(csvContent);
      const link = document.createElement("a");
      link.setAttribute("href", encodedUri);
      link.setAttribute("download", `payroll_payslips_${new Date().toISOString().slice(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Mode Selector */}
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
            {t("payslips.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("payslips.tabSubtitle")}
          </p>
        </div>

        {/* View Mode Segmented Controls */}
        <div
          style={{
            display: "inline-flex",
            padding: "0.25rem",
            background: "var(--color-surface-subtle)",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-default)",
            gap: "0.25rem"
          }}
        >
          <button
            type="button"
            onClick={() => {
              setViewMode("self");
              setPage(1);
            }}
            style={{
              padding: "0.375rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              border: "none",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer",
              background: viewMode === "self" ? "var(--color-surface-card)" : "transparent",
              color: viewMode === "self" ? "var(--color-content-primary)" : "var(--color-content-muted)",
              boxShadow: viewMode === "self" ? "var(--shadow-xs)" : "none",
              transition: "all 0.15s ease"
            }}
          >
            {t("payslips.modes.selfService")}
          </button>
          <button
            type="button"
            onClick={() => {
              setViewMode("company");
              setPage(1);
            }}
            style={{
              padding: "0.375rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              border: "none",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer",
              background: viewMode === "company" ? "var(--color-surface-card)" : "transparent",
              color: viewMode === "company" ? "var(--color-content-primary)" : "var(--color-content-muted)",
              boxShadow: viewMode === "company" ? "var(--shadow-xs)" : "none",
              transition: "all 0.15s ease"
            }}
          >
            {t("payslips.modes.companyPayslips")}
          </button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          padding: "1rem 1.25rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-md)",
          border: "1px solid var(--color-border-default)"
        }}
      >
        <div style={{ flex: 1, minWidth: "240px", maxWidth: "450px" }}>
          <TextField
            label={t("common.search")}
            value={searchTerm}
            onChange={(val) => {
              setSearchTerm(val);
              setPage(1);
            }}
            placeholder={t("payslips.searchPlaceholder")}
          />
        </div>

        <Button
          variant="secondary"
          size="compact"
          onPress={handleExportCsv}
          isDisabled={isExporting || displayItems.length === 0}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="7 10 12 15 17 10" />
            <line x1="12" y1="15" x2="12" y2="3" />
          </svg>
          {isExporting ? t("common.exporting", "جارٍ التصدير...") : t("reports.exportCsv")}
        </Button>
      </div>

      {/* Payslips Table */}
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
                  borderBottom: "1px solid var(--color-border-subtle)",
                  background: "var(--color-surface-subtle)",
                  textAlign: "start"
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.employee")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.grossEarnings")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.totalDeductions")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.netPay")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.employerCost")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.paymentStatus")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("payslips.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={7} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : displayItems.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: "3rem 2rem", textAlign: "center" }}>
                    <p style={{ margin: 0, fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("payslips.emptyTitle")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("payslips.emptyDesc")}
                    </p>
                  </td>
                </tr>
              ) : (
                displayItems.map((item: Payslip) => (
                  <tr
                    key={item.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {resolveEmployeeDisplay(item, employeesList, isAr)}
                      </div>
                      <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        #{item.payslipNumber}
                      </div>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <bdi>{formatMoneyAmount(item.gross, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-status-danger)" }}>
                      <bdi>{formatMoneyAmount(item.deductions > 0 ? -item.deductions : item.deductions, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                      <bdi>{formatMoneyAmount(item.net, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-secondary)" }}>
                      <bdi>{formatMoneyAmount(item.employerCost, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.status} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onPress={() => openPayslip(item)}
                        style={{ display: "inline-flex", alignItems: "center", gap: "0.375rem" }}
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                        {t("payslips.viewDetailsAction")}
                      </Button>
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
            {t("payslips.totalRecords", { count: totalRecords, defaultValue: `إجمالي السجلات: ${totalRecords}` })}
          </span>
          <Pagination
            page={page}
            totalPages={totalPages}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
          />
        </div>
      </div>

      {/* Detailed Modal */}
      <PayslipViewModal
        isOpen={modalOpen}
        onClose={() => {
          setModalOpen(false);
          setSelectedPayslip(null);
        }}
        payslip={selectedPayslip}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
