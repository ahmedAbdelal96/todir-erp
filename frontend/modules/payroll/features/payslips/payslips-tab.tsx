import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import {
  useSelfPayslips,
  usePayslips
} from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { PayslipViewModal } from "./payslip-view-modal.tsx";
import type { Payslip } from "../../types.ts";

export interface PayslipsTabProps {
  readonly legalEntityId: string;
}

export function PayslipsTab({
  legalEntityId
}: PayslipsTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const [viewMode, setViewMode] = useState<"self" | "company">("self");
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedPayslip, setSelectedPayslip] = useState<Payslip | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  // Self payslips query
  const selfQuery = useSelfPayslips(legalEntityId, { pageSize: 50 });

  // Company payslips query
  const companyQuery = usePayslips(legalEntityId, { pageSize: 50 });

  const displayItems: readonly Payslip[] =
    viewMode === "self"
      ? selfQuery.data ?? []
      : companyQuery.data ?? [];

  const isLoading = viewMode === "self" ? selfQuery.isLoading : companyQuery.isLoading;

  const filtered = displayItems.filter((item: Payslip) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      item.employmentId.toLowerCase().includes(term) ||
      item.payslipNumber.toLowerCase().includes(term)
    );
  });

  const openPayslip = (item: Payslip) => {
    setSelectedPayslip(item);
    setModalOpen(true);
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
            onClick={() => setViewMode("self")}
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
            onClick={() => setViewMode("company")}
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
            placeholder={t("payslips.searchPlaceholder")}
          />
        </div>
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
                  background: "var(--color-surface-subtle)",
                  borderBottom: "1px solid var(--color-border-default)",
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
              ) : filtered.length === 0 ? (
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
                filtered.map((item: Payslip) => (
                  <tr
                    key={item.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {item.employmentId}
                      </div>
                      <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        #{item.payslipNumber}
                      </div>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <bdi>{item.gross.toLocaleString()} EGP</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-status-danger)" }}>
                      <bdi>-{item.deductions.toLocaleString()} EGP</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                      <bdi>{item.net.toLocaleString()} EGP</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-secondary)" }}>
                      <bdi>{item.employerCost.toLocaleString()} EGP</bdi>
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
      </div>

      {/* Detailed Modal */}
      <PayslipViewModal
        isOpen={modalOpen}
        onClose={() => {
          setModalOpen(false);
          setSelectedPayslip(null);
        }}
        payslip={selectedPayslip}
      />
    </div>
  );
}
