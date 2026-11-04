import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { usePayrollGroups, usePayrollPeriods } from "../../hooks/use-payroll-queries.ts";
import { NewPayrollGroupModal } from "./new-payroll-group-modal.tsx";
import { NewPayrollPeriodModal } from "./new-payroll-period-modal.tsx";
import type { PayrollGroup, PayrollPeriod } from "../../types.ts";

export interface PayrollSetupTabProps {
  readonly legalEntityId: string;
}

export function PayrollSetupTab({ legalEntityId }: PayrollSetupTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [isNewGroupOpen, setIsNewGroupOpen] = useState(false);
  const [isNewPeriodOpen, setIsNewPeriodOpen] = useState(false);

  const groupsQuery = usePayrollGroups(legalEntityId);
  const groups = groupsQuery.data ?? [];

  // Effective selected group
  const activeGroupId = selectedGroupId ?? groups[0]?.id ?? "";
  const periodsQuery = usePayrollPeriods(legalEntityId, activeGroupId);
  const periods = periodsQuery.data ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Groups Surface */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("setup.groupsSectionTitle")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("setup.groupsSectionSubtitle")}
            </p>
          </div>

          <Button variant="primary" onPress={() => setIsNewGroupOpen(true)}>
            + {t("setup.newGroupAction")}
          </Button>
        </div>

        {groups.length === 0 ? (
          <div style={{ textAlign: "center", padding: "2.5rem 1rem", color: "var(--color-content-muted)" }}>
            <p>{t("setup.groups.emptyTitle")}</p>
            <p style={{ fontSize: "0.8125rem" }}>{t("setup.groups.emptyDesc")}</p>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.code")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.name")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.frequency")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.payFrequency")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("common.currency")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.holiday")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("common.status")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {groups.map((g: PayrollGroup) => {
                  const isSelected = g.id === activeGroupId;
                  return (
                    <tr
                      key={g.id}
                      style={{
                        borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                        background: isSelected ? "rgba(16, 42, 67, 0.04)" : "transparent"
                      }}
                    >
                      <td style={{ padding: "0.875rem 1rem", fontWeight: 600, color: "var(--color-brand-primary, #102a43)" }}>
                        {g.code}
                      </td>
                      <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{g.name}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{g.frequency}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{g.payDay}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{g.currencyCode}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{g.holidayAdjustment}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>
                        <StatusBadge status={g.status === "Active" ? "success" : "neutral"} label={g.status} />
                      </td>
                      <td style={{ padding: "0.875rem 1rem" }}>
                        <Button
                          variant={isSelected ? "primary" : "secondary"}
                          size="compact"
                          onPress={() => setSelectedGroupId(g.id)}
                        >
                          {t("setup.selectGroup")}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Periods Surface for Selected Group */}
      {activeGroupId && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
            <div>
              <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("setup.periodsSectionTitle")}
              </h3>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("setup.periodsSectionSubtitle")}
              </p>
            </div>

            <Button variant="secondary" onPress={() => setIsNewPeriodOpen(true)}>
              + {t("setup.newPeriodAction")}
            </Button>
          </div>

          {periods.length === 0 ? (
            <div style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
              <p>{t("setup.periods.emptyTitle")}</p>
              <p style={{ fontSize: "0.8125rem" }}>{t("setup.periods.emptyDesc")}</p>
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periodStartDate")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periodEndDate")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periods.cutoffDate")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periods.payDate")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periods.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {periods.map((p: PayrollPeriod) => (
                    <tr key={p.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{p.periodStart}</td>
                      <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{p.periodEnd}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{p.inputCutoff}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>{p.plannedPayDate}</td>
                      <td style={{ padding: "0.875rem 1rem" }}>
                        <StatusBadge
                          status={p.status === "Open" ? "info" : p.status === "Finalized" ? "success" : "neutral"}
                          label={p.status}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* Salary Structures Integration Info Banner */}
      <Surface style={{ padding: "1.25rem", background: "var(--color-surface-subtle, #f8fafc)" }}>
        <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
          {t("setup.salaryStructuresNoticeTitle")}
        </h4>
        <p style={{ margin: "0.375rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)", lineHeight: 1.5 }}>
          {t("setup.salaryStructuresNoticeDescription")}
        </p>
      </Surface>

      {/* Modals */}
      <NewPayrollGroupModal
        isOpen={isNewGroupOpen}
        onClose={() => setIsNewGroupOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewPayrollPeriodModal
        isOpen={isNewPeriodOpen}
        onClose={() => setIsNewPeriodOpen(false)}
        legalEntityId={legalEntityId}
        defaultGroupId={activeGroupId}
      />
    </div>
  );
}
