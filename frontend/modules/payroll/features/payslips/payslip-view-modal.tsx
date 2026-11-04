import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { useEmployees } from "../../../hr/public.ts";
import { formatMoneyAmount, resolveEmployeeLabel } from "../../utils/payroll-formatters.ts";
import type { Payslip } from "../../types.ts";

export interface PayslipViewModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly payslip: Payslip | null;
  readonly legalEntityId?: string;
}

export function PayslipViewModal({
  isOpen,
  onClose,
  payslip,
  legalEntityId
}: PayslipViewModalProps): React.JSX.Element | null {
  const { t } = useTranslation("payroll");
  const employeesListQuery = useEmployees(legalEntityId ? { legalEntityId } : undefined);
  const employeesList = employeesListQuery.data?.items ?? [];

  if (!payslip) return null;

  const handlePrint = () => {
    window.print();
  };

  const employeeLabel = resolveEmployeeLabel(payslip.employmentId, employeesList);

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("payslips.modalTitle")}
      size="large"
      footer={
        <>
          <Button variant="secondary" onPress={onClose}>
            {t("actions.close")}
          </Button>
          <Button variant="primary" onPress={handlePrint} style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="6 9 6 2 18 2 18 9" />
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
              <rect x="6" y="14" width="12" height="8" />
            </svg>
            {t("payslips.printAction")}
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {/* Header Summary Card */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem",
            padding: "1.25rem 1.5rem",
            background: "var(--color-surface-subtle)",
            borderRadius: "var(--radius-lg)",
            border: "1px solid var(--color-border-default)"
          }}
        >
          <div>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {employeeLabel}
            </div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
              {t("payslips.payslipNumber")}: #{payslip.payslipNumber}
              {payslip.finalizedAt && ` • ${new Date(payslip.finalizedAt).toLocaleDateString()}`}
            </div>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <PayrollStatusBadge status={payslip.status} />
          </div>
        </div>

        {/* Highlight Net Pay Card */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
            gap: "0.75rem",
            padding: "1rem 1.25rem",
            background: "var(--color-brand-primary-subtle)",
            border: "1px solid var(--color-brand-primary)",
            borderRadius: "var(--radius-lg)"
          }}
        >
          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
              {t("payslips.grossEarnings")}
            </span>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)", marginTop: "0.25rem" }}>
              <bdi>{formatMoneyAmount(payslip.gross, "EGP")}</bdi>
            </div>
          </div>

          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
              {t("payslips.totalDeductions")}
            </span>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-danger)", marginTop: "0.25rem" }}>
              <bdi>{formatMoneyAmount(payslip.deductions > 0 ? -payslip.deductions : payslip.deductions, "EGP")}</bdi>
            </div>
          </div>

          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
              {t("payslips.netPay")}
            </span>
            <div style={{ fontSize: "1.375rem", fontWeight: 800, color: "var(--color-status-success)", marginTop: "0.25rem" }}>
              <bdi>{formatMoneyAmount(payslip.net, "EGP")}</bdi>
            </div>
          </div>

          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
              {t("payslips.employerCost")}
            </span>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-secondary)", marginTop: "0.25rem" }}>
              <bdi>{formatMoneyAmount(payslip.employerCost, "EGP")}</bdi>
            </div>
          </div>
        </div>

        {/* Itemized Gross-to-Net Breakdown Tables */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" }}>
          {/* Earnings Breakdown */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-default)",
              background: "var(--color-surface-card)"
            }}
          >
            <h4 style={{ margin: "0 0 0.75rem 0", fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("payslips.grossEarnings")} (Earnings)
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.8125rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "0.375rem 0", borderBottom: "1px dashed var(--color-border-subtle)" }}>
                <span>{t("components.types.baseSalary", "الراتب الأساسي")}</span>
                <bdi style={{ fontWeight: 600 }}>{formatMoneyAmount(payslip.gross, "EGP")}</bdi>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", paddingTop: "0.5rem", fontWeight: 700 }}>
                <span>{t("runs.workspace.totalGross", "إجمالي الاستحقاقات")}</span>
                <bdi style={{ color: "var(--color-content-primary)" }}>{formatMoneyAmount(payslip.gross, "EGP")}</bdi>
              </div>
            </div>
          </div>

          {/* Deductions Breakdown */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-default)",
              background: "var(--color-surface-card)"
            }}
          >
            <h4 style={{ margin: "0 0 0.75rem 0", fontSize: "0.875rem", fontWeight: 700, color: "var(--color-status-danger)" }}>
              {t("payslips.totalDeductions")} (Deductions)
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.8125rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "0.375rem 0", borderBottom: "1px dashed var(--color-border-subtle)" }}>
                <span>{t("components.types.statutoryDeductions", "الاستقطاعات الإلزامية والضريبية")}</span>
                <bdi style={{ fontWeight: 600, color: "var(--color-status-danger)" }}>
                  {formatMoneyAmount(payslip.deductions > 0 ? -payslip.deductions : payslip.deductions, "EGP")}
                </bdi>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", paddingTop: "0.5rem", fontWeight: 700 }}>
                <span>{t("runs.workspace.totalDeductions", "إجمالي الاستقطاعات")}</span>
                <bdi style={{ color: "var(--color-status-danger)" }}>
                  {formatMoneyAmount(payslip.deductions > 0 ? -payslip.deductions : payslip.deductions, "EGP")}
                </bdi>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Dialog>
  );
}
