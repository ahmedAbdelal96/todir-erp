import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { DateTimeDisplay } from "../../../../components/fields/date-time-display.tsx";
import { CloseShieldIcon, AlertTriangleIcon, CheckCircleIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import type { CloseView, ProjectCloseExceptionView, ProjectDetail } from "../../types.ts";

export interface ProjectClosingTabProps {
  readonly project: ProjectDetail;
  readonly closeReadiness?: CloseView | null | undefined;
  readonly closeExceptions: readonly ProjectCloseExceptionView[];
  readonly isLoading: boolean;
  readonly onAddException: () => void;
  readonly onResolveException: (exceptionId: string) => void;
  readonly onCloseProject: () => void;
  readonly onReopenProject: () => void;
}

export function ProjectClosingTab({
  project,
  closeReadiness,
  closeExceptions,
  isLoading,
  onAddException,
  onResolveException,
  onCloseProject,
  onReopenProject
}: ProjectClosingTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  const getExceptionLabel = (exceptionType: string): string => {
    switch (exceptionType) {
      case "TOLERANCE_OVERRUN":
        return t("closeExceptionTypes.TOLERANCE_OVERRUN");
      case "UNAPPROVED_TIME":
        return t("closeExceptionTypes.UNAPPROVED_TIME");
      case "OPEN_COMMITMENT":
        return t("closeExceptionTypes.OPEN_COMMITMENT");
      case "PENDING_DELIVERABLE":
        return t("closeExceptionTypes.PENDING_DELIVERABLE");
      case "UNBILLED_CHARGE":
        return t("closeExceptionTypes.UNBILLED_CHARGE");
      case "OUTSTANDING_AR":
        return t("closeExceptionTypes.OUTSTANDING_AR");
      case "MANUAL_OVERRIDE":
        return t("closeExceptionTypes.MANUAL_OVERRIDE");
      case "Operational":
        return t("closeExceptionTypes.Operational");
      default:
        return exceptionType;
    }
  };

  const blockers = closeReadiness?.blockers ?? [];
  const isReady = blockers.length === 0 && project.lifecycleState === "ExecutionCompleted";
  const isClosed = project.lifecycleState === "Closed";

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
          <CloseShieldIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.closing")}
          </h2>
        </div>

        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button variant="secondary" size="compact" onClick={onAddException} data-testid="btn-add-exception">
            <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
            {t("actions.addCloseException")}
          </Button>
          {isClosed ? (
            <Button variant="secondary" size="compact" onClick={onReopenProject} data-testid="btn-reopen-proj">
              {t("actions.reopenProject")}
            </Button>
          ) : (
            <Button
              variant="primary"
              size="compact"
              onClick={onCloseProject}
              isDisabled={blockers.length > 0}
              data-testid="btn-close-project"
            >
              {t("actions.closeProject")}
            </Button>
          )}
        </div>
      </Surface>

      {/* Readiness Status Card */}
      <Surface
        variant="card"
        style={{
          padding: "1.5rem",
          background: isReady ? "#f0fdf4" : blockers.length > 0 ? "#fef2f2" : "var(--color-surface-card, #ffffff)",
          border: `1px solid ${isReady ? "#86efac" : blockers.length > 0 ? "#fca5a5" : "var(--color-border-default, #cbd5e1)"}`,
          borderRadius: "var(--radius-lg, 12px)",
          display: "flex",
          flexDirection: "column",
          gap: "0.75rem"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          {isReady ? (
            <CheckCircleIcon size={24} color="#059669" />
          ) : blockers.length > 0 ? (
            <AlertTriangleIcon size={24} color="#dc2626" />
          ) : (
            <CloseShieldIcon size={24} color="#64748b" />
          )}

          <div>
            <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: isReady ? "#065f46" : blockers.length > 0 ? "#991b1b" : "#0f172a" }}>
              {isClosed ? t("states.Closed") : isReady ? t("messages.closeReady") : t("messages.closeBlocked")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: isReady ? "#047857" : blockers.length > 0 ? "#b91c1c" : "#64748b" }}>
              {isClosed
                ? t("messages.operationSuccess")
                : isReady
                ? t("messages.closeReadyDesc")
                : t("messages.closeBlockedDesc")}
            </p>
          </div>
        </div>

        {/* Blockers list if any */}
        {blockers.length > 0 && (
          <div style={{ background: "#ffffff", padding: "1rem", borderRadius: "var(--radius-md, 8px)", border: "1px solid #fecaca", marginTop: "0.5rem" }}>
            <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", fontWeight: 700, color: "#991b1b" }}>
              {t("tables.blocker")} ({blockers.length}):
            </h4>
            <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.875rem", color: "#b91c1c" }}>
              {blockers.map((b) => (
                <li key={b} style={{ marginBottom: "0.25rem" }}>{b}</li>
              ))}
            </ul>
          </div>
        )}
      </Surface>

      {/* Close Exceptions Table */}
      <Surface
        variant="card"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflow: "hidden"
        }}
      >
        <div style={{ padding: "1rem 1.25rem", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
          <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("fields.exceptionType")} ({closeExceptions.length})
          </h3>
        </div>

        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("actions.refresh")}...
          </div>
        ) : closeExceptions.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("messages.noExceptions")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.exceptionType")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.exceptionMessage")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.resolution")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {closeExceptions.map((ex) => (
                  <tr key={ex.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <StatusBadge
                        intent="neutral"
                        label={getExceptionLabel(ex.exceptionType)}
                        size="small"
                      />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>{ex.message}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {ex.resolvedAt ? (
                        <DateTimeDisplay value={ex.resolvedAt} type="datetime" />
                      ) : (
                        <span style={{ color: "var(--color-status-danger, #dc2626)", fontWeight: 600 }}>{t("states.open")}</span>
                      )}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      {!ex.resolvedAt && (
                        <Button variant="secondary" size="compact" onClick={() => onResolveException(ex.id)} data-testid="btn-resolve-exception">
                          {t("actions.resolveException")}
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
