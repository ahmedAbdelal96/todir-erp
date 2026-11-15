import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge, type StatusIntent } from "../../../../components/fields/status-badge.tsx";
import { ProjectMoney } from "../../components/project-money.tsx";
import { ClockTimeIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import type { TimeEntryView, TimeActionKind } from "../../types.ts";

export interface TimesheetsTabProps {
  readonly timeEntries: readonly TimeEntryView[];
  readonly isLoading: boolean;
  readonly currencyCode?: string | null;
  readonly onLogTime: () => void;
  readonly onTimeAction: (timeId: string, action: TimeActionKind, version: number) => void;
}

export function TimesheetsTab({
  timeEntries,
  isLoading,
  currencyCode = "EGP",
  onLogTime,
  onTimeAction
}: TimesheetsTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  function timeStateIntent(state: string): StatusIntent {
    switch (state) {
      case "Finalized":
        return "success";
      case "Approved":
        return "info";
      case "Submitted":
        return "warning";
      case "Rejected":
        return "danger";
      case "Corrected":
        return "neutral";
      default:
        return "neutral";
    }
  }

  const currency = currencyCode || "EGP";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Bar */}
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
          <ClockTimeIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.time")}
          </h2>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
            ({timeEntries.length} {t("tables.date")})
          </span>
        </div>

        <Button variant="primary" size="compact" onClick={onLogTime} data-testid="btn-log-time">
          <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
          {t("actions.logTime")}
        </Button>
      </Surface>

      {/* Table */}
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
        ) : timeEntries.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("messages.noTimeEntries")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.date")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.duration")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.billable")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("fields.costAmount")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("fields.billingAmount")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {timeEntries.map((entry) => {
                  const hours = (entry.durationMinutes / 60).toFixed(1);
                  return (
                    <tr key={entry.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        <div>{entry.workDate}</div>
                        {entry.notes && (
                          <div style={{ fontSize: "0.75rem", fontWeight: 400, color: "var(--color-content-muted, #64748b)" }}>
                            {entry.notes}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {entry.durationMinutes} {t("fields.durationMinutes")}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {entry.billable ? (
                          <StatusBadge intent="info" label={t("fields.billable")} size="small" />
                        ) : (
                          <span style={{ color: "var(--color-content-subtle, #94a3b8)" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge intent={timeStateIntent(entry.state)} label={t(`timeStates.${entry.state}` as const)} size="small" />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {entry.costAmount !== null ? (
                          <ProjectMoney amount={entry.costAmount} currency={currency} />
                        ) : (
                          <span style={{ color: "var(--color-content-subtle, #94a3b8)" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {entry.billingAmount !== null ? (
                          <ProjectMoney amount={entry.billingAmount} currency={currency} />
                        ) : (
                          <span style={{ color: "var(--color-content-subtle, #94a3b8)" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <div style={{ display: "flex", gap: "0.35rem", justifyContent: "flex-end" }}>
                          {entry.state === "Draft" && (
                            <Button variant="secondary" size="compact" onClick={() => onTimeAction(entry.id, "submit", entry.version)}>
                              {t("actions.submit")}
                            </Button>
                          )}
                          {entry.state === "Submitted" && (
                            <>
                              <Button variant="primary" size="compact" onClick={() => onTimeAction(entry.id, "approve", entry.version)}>
                                {t("actions.approve")}
                              </Button>
                              <Button variant="secondary" size="compact" onClick={() => onTimeAction(entry.id, "reject", entry.version)}>
                                {t("actions.reject")}
                              </Button>
                            </>
                          )}
                          {entry.state === "Approved" && (
                            <Button variant="primary" size="compact" onClick={() => onTimeAction(entry.id, "finalize", entry.version)}>
                              {t("actions.finalize")}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>
    </div>
  );
}
