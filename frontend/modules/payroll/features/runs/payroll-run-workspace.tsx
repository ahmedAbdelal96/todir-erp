import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { PayrollKpiCard } from "../../components/payroll-kpi-card.tsx";
import {
  usePayrollRun,
  usePayrollRunEmployees,
  useCaptureInputsMutation,
  useCalculateRunMutation,
  useApproveRunMutation,
  useFinalizeRunMutation
} from "../../hooks/use-payroll-queries.ts";
import { useEmployees } from "../../../hr/public.ts";
import { formatMoneyAmount, resolveEmployeeLabel } from "../../utils/payroll-formatters.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface PayrollRunWorkspaceProps {
  readonly legalEntityId: string;
  readonly runId: string;
  readonly onBack: () => void;
}

export function PayrollRunWorkspace({
  legalEntityId,
  runId,
  onBack
}: PayrollRunWorkspaceProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const [search, setSearch] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [isFinalizeConfirmOpen, setIsFinalizeConfirmOpen] = useState(false);

  const runQuery = usePayrollRun(legalEntityId, runId);
  const employeesQuery = usePayrollRunEmployees(
    legalEntityId,
    runId,
    search.trim() ? { search: search.trim(), pageSize: 100 } : { pageSize: 100 }
  );

  const captureMutation = useCaptureInputsMutation(legalEntityId, runId);
  const calculateMutation = useCalculateRunMutation(legalEntityId, runId);
  const approveMutation = useApproveRunMutation(legalEntityId, runId);
  const finalizeMutation = useFinalizeRunMutation(legalEntityId, runId);

  const run = runQuery.data;
  const employees = employeesQuery.data ?? [];
  const employeesListQuery = useEmployees({ legalEntityId });
  const employeesList = employeesListQuery.data?.items ?? [];

  const handleCapture = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await captureMutation.mutateAsync();
      setActionSuccess(t("runs.workspace.captureAction"));
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleCalculate = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await calculateMutation.mutateAsync();
      setActionSuccess(t("runs.workspace.calculateAction"));
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleApprove = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await approveMutation.mutateAsync();
      setActionSuccess(t("runs.workspace.approveAction"));
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleFinalize = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await finalizeMutation.mutateAsync();
      setIsFinalizeConfirmOpen(false);
      setActionSuccess(t("runs.workspace.finalizeAction"));
    } catch (err: unknown) {
      setIsFinalizeConfirmOpen(false);
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  if (runQuery.isLoading) {
    return (
      <Surface style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading")}
      </Surface>
    );
  }

  if (!run) {
    return (
      <Surface style={{ padding: "2rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)" }}>{t("runs.emptyTitle")}</p>
        <Button variant="secondary" onPress={onBack} style={{ marginTop: "1rem" }}>
          {t("runs.workspace.backAction")}
        </Button>
      </Surface>
    );
  }

  const isFinalized = run.finalizationState === "Finalized";
  const isCalculated = run.calculationState === "Calculated";
  const hasExceptions = employees.some((e) => e.status === "Exception" || Boolean(e.exceptionCode));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Workspace Top Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          <Button variant="secondary" size="compact" onPress={onBack}>
            ← {t("runs.workspace.backAction")}
          </Button>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
              <h2 style={{ margin: 0, fontSize: "1.375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {run.runNumber}
              </h2>
              <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                ({run.runType})
              </span>
            </div>
            <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.25rem" }}>
              <PayrollStatusBadge status={run.calculationState} type="calculation" />
              <PayrollStatusBadge status={run.finalizationState} type="finalization" />
              <PayrollStatusBadge status={run.paymentState} type="payment" />
            </div>
          </div>
        </div>

        {/* Action Controls */}
        {!isFinalized && (
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            <Button
              variant="secondary"
              size="compact"
              onPress={handleCapture}
              isLoading={captureMutation.isPending}
            >
              {t("runs.workspace.captureAction")}
            </Button>

            <Button
              variant="secondary"
              size="compact"
              onPress={handleCalculate}
              isLoading={calculateMutation.isPending}
            >
              {t("runs.workspace.calculateAction")}
            </Button>

            <Button
              variant="secondary"
              size="compact"
              onPress={handleApprove}
              isLoading={approveMutation.isPending}
              isDisabled={!isCalculated || hasExceptions}
            >
              {t("runs.workspace.approveAction")}
            </Button>

            <Button
              variant="primary"
              size="compact"
              onPress={() => setIsFinalizeConfirmOpen(true)}
              isLoading={finalizeMutation.isPending}
              isDisabled={!isCalculated || hasExceptions}
            >
              {t("runs.workspace.finalizeAction")}
            </Button>
          </div>
        )}
      </div>

      {/* Notifications */}
      {actionSuccess && (
        <div
          role="status"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-success-subtle, #dcfce7)",
            color: "var(--color-status-success, #15803d)",
            border: "1px solid var(--color-status-success-border, #bbf7d0)",
            fontSize: "0.875rem"
          }}
        >
          {actionSuccess}
        </div>
      )}

      {actionError && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger, #b91c1c)",
            border: "1px solid var(--color-status-danger-border, #fecaca)",
            fontSize: "0.875rem"
          }}
        >
          {actionError}
        </div>
      )}

      {hasExceptions && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-warning-subtle, #fef3c7)",
            color: "var(--color-status-warning-content, #92400e)",
            border: "1px solid var(--color-status-warning-border, #fde68a)",
            fontSize: "0.875rem"
          }}
        >
          {t("runs.workspace.exceptionsNotice")}
        </div>
      )}

      {/* Run Financial Summary KPIs */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <PayrollKpiCard
          title={t("runs.gross")}
          value={<bdi>{run.totalGross.toLocaleString()} EGP</bdi>}
          subtitle={t("runs.workspace.grossEarnings")}
          tone="default"
        />

        <PayrollKpiCard
          title={t("runs.workspace.deductions")}
          value={<bdi>-{run.totalDeductions.toLocaleString()} EGP</bdi>}
          subtitle={t("runs.workspace.deductions")}
          tone="warning"
        />

        <PayrollKpiCard
          title={t("runs.net")}
          value={<bdi>{run.totalNet.toLocaleString()} EGP</bdi>}
          subtitle={t("runs.workspace.netPay")}
          tone="primary"
        />

        <PayrollKpiCard
          title={t("reports.employees")}
          value={run.employeeCount}
          subtitle={t("runs.workspace.employee")}
          tone="success"
        />
      </div>

      {/* Employee Breakdown Table Surface */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("runs.workspace.itemsTitle")}
            </h3>
            <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("runs.workspace.itemsSubtitle")}
            </p>
          </div>

          <div style={{ width: "280px" }}>
            <TextField
              value={search}
              onChange={setSearch}
              placeholder={t("runs.workspace.searchEmployeesPlaceholder")}
              aria-label={t("runs.workspace.searchEmployeesPlaceholder")}
            />
          </div>
        </div>

        {employees.length === 0 ? (
          <div style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
            <p>{t("runs.workspace.emptyEmployeesTitle")}</p>
            <p style={{ fontSize: "0.8125rem" }}>{t("runs.workspace.emptyEmployeesDesc")}</p>
            {!isFinalized && (
              <Button variant="secondary" size="compact" onPress={handleCapture} style={{ marginTop: "0.5rem" }}>
                {t("runs.workspace.captureAction")}
              </Button>
            )}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.employee")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.grossEarnings")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.deductions")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.netPay")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.employerCost")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.calcState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.exceptionsTitle")}</th>
                </tr>
              </thead>
              <tbody>
                {employees.map((emp, index) => (
                  <tr key={emp.employmentId || index} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {resolveEmployeeLabel(emp.employmentId, employeesList)}
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <bdi>{formatMoneyAmount(emp.gross, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.875rem 1rem", color: "var(--color-status-danger)" }}>
                      <bdi>{formatMoneyAmount(emp.deductions > 0 ? -emp.deductions : emp.deductions, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                      <bdi>{formatMoneyAmount(emp.net, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.875rem 1rem", color: "var(--color-content-secondary)" }}>
                      <bdi>{formatMoneyAmount(emp.employerCost, "EGP")}</bdi>
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <PayrollStatusBadge status={emp.status} type="calculation" />
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      {emp.exceptionCode ? (
                        <span style={{ color: "var(--color-status-danger)", fontWeight: 600, fontSize: "0.8125rem" }}>
                          {emp.exceptionCode}
                        </span>
                      ) : (
                        <span style={{ color: "var(--color-status-success)", fontSize: "0.8125rem" }}>
                          ✓
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Finalize Confirmation Safe Dialog */}
      <Dialog
        isOpen={isFinalizeConfirmOpen}
        onOpenChange={setIsFinalizeConfirmOpen}
        title={t("runs.workspace.finalizeConfirmTitle")}
        description={t("runs.workspace.finalizeConfirmDesc")}
        size="medium"
        footer={
          <>
            <Button variant="secondary" onPress={() => setIsFinalizeConfirmOpen(false)} isDisabled={finalizeMutation.isPending}>
              {t("actions.cancel")}
            </Button>
            <Button variant="primary" onPress={handleFinalize} isLoading={finalizeMutation.isPending}>
              {t("runs.workspace.finalizeConfirmAction")}
            </Button>
          </>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.875rem" }}>
          <p style={{ margin: 0 }}>
            {t("runs.workspace.finalizeConfirmDesc")}
          </p>
        </div>
      </Dialog>
    </div>
  );
}
