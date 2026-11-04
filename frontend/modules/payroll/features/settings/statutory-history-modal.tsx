import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { usePayrollStatutoryHistory } from "../../hooks/use-payroll-queries.ts";
import type { PayrollStatutorySettings } from "../../types.ts";

export interface StatutoryHistoryModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function StatutoryHistoryModal({
  isOpen,
  onClose,
  legalEntityId
}: StatutoryHistoryModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const historyQuery = usePayrollStatutoryHistory(legalEntityId, { pageSize: 50 });
  const history = historyQuery.data ?? [];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("settings.historyModalTitle")}
      size="large"
      footer={
        <Button variant="secondary" onPress={onClose}>
          {t("actions.close")}
        </Button>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {historyQuery.isLoading ? (
          <p style={{ textAlign: "center", color: "var(--color-content-muted)" }}>{t("common.loading")}</p>
        ) : history.length === 0 ? (
          <p style={{ textAlign: "center", color: "var(--color-content-muted)" }}>{t("settings.noHistory")}</p>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.versionCol")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.effectiveDateCol")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.taxModeCol")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.insuranceModeCol")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.policyCol")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("settings.createdAtCol")}</th>
                </tr>
              </thead>
              <tbody>
                {history.map((item: PayrollStatutorySettings) => (
                  <tr key={item.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      {t("settings.version", { version: item.version })}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>{item.effectiveFrom}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.taxMode} type="statutory" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.insuranceMode} type="statutory" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {item.manualInputPolicy === "Block" ? t("settings.policy.block") : t("settings.policy.skip")}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted)" }}>
                      {new Date(item.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Dialog>
  );
}
