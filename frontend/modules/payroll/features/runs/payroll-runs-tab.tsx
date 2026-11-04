import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { usePayrollRuns } from "../../hooks/use-payroll-queries.ts";
import { NewPayrollRunModal } from "./new-payroll-run-modal.tsx";
import { PayrollRunWorkspace } from "./payroll-run-workspace.tsx";
import type { PayrollRun } from "../../types.ts";

export interface PayrollRunsTabProps {
  readonly legalEntityId: string;
  readonly initialRunId?: string | null;
  readonly onOpenRun?: (runId: string) => void;
}

export function PayrollRunsTab({ legalEntityId, initialRunId, onOpenRun }: PayrollRunsTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const [selectedRunId, setSelectedRunId] = useState<string | null>(initialRunId ?? null);
  const [isNewRunOpen, setIsNewRunOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [runTypeFilter, setRunTypeFilter] = useState<string>("ALL");
  const [stateFilter, setStateFilter] = useState<string>("ALL");

  const queryParams: { page?: number; pageSize?: number; search?: string; runType?: string; state?: string } = {
    pageSize: 50
  };
  if (search.trim()) queryParams.search = search.trim();
  if (runTypeFilter !== "ALL") queryParams.runType = runTypeFilter;
  if (stateFilter !== "ALL") queryParams.state = stateFilter;

  const runsQuery = usePayrollRuns(legalEntityId, queryParams);
  const runs = runsQuery.data ?? [];

  if (selectedRunId) {
    return (
      <PayrollRunWorkspace
        legalEntityId={legalEntityId}
        runId={selectedRunId}
        onBack={() => setSelectedRunId(null)}
      />
    );
  }

  const runTypeOptions = [
    { id: "ALL", label: t("common.allRunTypes") },
    { id: "Regular", label: t("runs.types.regular") },
    { id: "OffCycle", label: t("runs.types.offCycle") },
    { id: "BonusOnly", label: t("runs.types.bonusOnly") },
    { id: "Retro", label: t("runs.types.retro") },
    { id: "FinalSettlement", label: t("runs.types.finalSettlement") }
  ];

  const stateOptions = [
    { id: "ALL", label: t("common.allStatuses") },
    { id: "Draft", label: t("statuses.draft") },
    { id: "Calculated", label: t("statuses.calculated") },
    { id: "Finalized", label: t("statuses.finalized") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Header & Search Bar */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("runs.tabTitle")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("runs.tabSubtitle")}
            </p>
          </div>

          <Button variant="primary" onPress={() => setIsNewRunOpen(true)}>
            + {t("runs.newRunAction")}
          </Button>
        </div>

        {/* Filter Controls Row */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem", alignItems: "end" }}>
          <TextField
            label={t("common.search")}
            value={search}
            onChange={setSearch}
            placeholder={t("runs.searchPlaceholder")}
          />

          <Select
            label={t("common.runType")}
            selectedKey={runTypeFilter}
            onSelectionChange={(k) => setRunTypeFilter(String(k))}
            options={runTypeOptions}
          />

          <Select
            label={t("common.status")}
            selectedKey={stateFilter}
            onSelectionChange={(k) => setStateFilter(String(k))}
            options={stateOptions}
          />
        </div>
      </Surface>

      {/* Runs Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {runs.length === 0 ? (
          <div style={{ textAlign: "center", padding: "3rem 1rem", color: "var(--color-content-muted)" }}>
            <p style={{ fontSize: "1rem", fontWeight: 500 }}>{t("runs.emptyTitle")}</p>
            <p style={{ fontSize: "0.8125rem" }}>{t("runs.emptyDesc")}</p>
            <Button variant="secondary" size="compact" onPress={() => setIsNewRunOpen(true)} style={{ marginTop: "0.75rem" }}>
              {t("runs.newRunAction")}
            </Button>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.period")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.type")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("reports.employees")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.gross")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.workspace.deductions")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.net")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("common.status")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.actionsTitle")}</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r: PayrollRun) => (
                  <tr key={r.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600, color: "var(--color-brand-primary, #102a43)" }}>
                      {r.runNumber}
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>{r.runType}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>{r.employeeCount}</td>
                    <td style={{ padding: "0.875rem 1rem" }}><bdi>{(r.totalGross ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem" }}><bdi>-{(r.totalDeductions ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 700 }}><bdi>{(r.totalNet ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap" }}>
                        <PayrollStatusBadge status={r.calculationState} type="calculation" />
                        <PayrollStatusBadge status={r.finalizationState} type="finalization" />
                      </div>
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onPress={() => {
                          if (onOpenRun) {
                            onOpenRun(r.id);
                          } else {
                            setSelectedRunId(r.id);
                          }
                        }}
                      >
                        {t("home.viewWorkspaceAction")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Creation Modal */}
      <NewPayrollRunModal
        isOpen={isNewRunOpen}
        onClose={() => setIsNewRunOpen(false)}
        legalEntityId={legalEntityId}
        onCreated={(runId) => {
          setIsNewRunOpen(false);
          setSelectedRunId(runId);
        }}
      />
    </div>
  );
}
