import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge, type StatusIntent } from "../../../../components/fields/status-badge.tsx";
import { ChangeOrderIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import type { ChangeRequestView } from "../../types.ts";

export interface ChangeOrdersTabProps {
  readonly changeRequests: readonly ChangeRequestView[];
  readonly isLoading: boolean;
  readonly onNewChangeRequest: () => void;
  readonly onCreateBaseline: () => void;
  readonly onDecideChange: (change: ChangeRequestView) => void;
}

export function ChangeOrdersTab({
  changeRequests,
  isLoading,
  onNewChangeRequest,
  onCreateBaseline,
  onDecideChange
}: ChangeOrdersTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  function changeStatusIntent(status: string): StatusIntent {
    switch (status) {
      case "Approved":
        return "success";
      case "Draft":
        return "neutral";
      case "Submitted":
        return "warning";
      case "Rejected":
        return "danger";
      default:
        return "neutral";
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Action Bar */}
      <Surface
        variant="card"
        style={{
          padding: "0.75rem 1.25rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "0.75rem"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <ChangeOrderIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.changes")}
          </h2>
        </div>

        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="secondary" size="compact" onClick={onCreateBaseline} data-testid="btn-create-baseline">
            {t("actions.createBaseline")}
          </Button>
          <Button variant="primary" size="compact" onClick={onNewChangeRequest} data-testid="btn-new-change">
            <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
            {t("actions.createChange")}
          </Button>
        </div>
      </Surface>

      {/* Changes Table */}
      <Surface
        variant="card"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflow: "hidden"
        }}
      >
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("actions.refresh")}...
          </div>
        ) : changeRequests.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("messages.noChanges")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.requestNumber")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.title")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("fields.contractDelta")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("fields.budgetDelta")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("fields.scheduleDeltaDays")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {changeRequests.map((change) => (
                  <tr key={change.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontFamily: "monospace", fontWeight: 700, color: "var(--color-brand-primary, #2563eb)" }}>
                      {change.requestNumber}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {change.title}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <StatusBadge intent={changeStatusIntent(change.status)} label={t(`changeStatuses.${change.status}` as const)} size="small" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600, color: change.contractDelta >= 0 ? "var(--color-status-success, #059669)" : "var(--color-status-danger, #dc2626)" }}>
                      {change.contractDelta}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600, color: change.budgetDelta >= 0 ? "var(--color-status-success, #059669)" : "var(--color-status-danger, #dc2626)" }}>
                      {change.budgetDelta}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", color: "var(--color-content-muted, #64748b)" }}>
                      {change.scheduleDeltaDays}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      {change.status !== "Approved" && change.status !== "Rejected" && (
                        <Button variant="secondary" size="compact" onClick={() => onDecideChange(change)}>
                          {t("actions.decideChange")}
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>
    </div>
  );
}
