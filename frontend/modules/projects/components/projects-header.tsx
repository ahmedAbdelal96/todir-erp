import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { ProjectStatusBadge } from "./project-status-badge.tsx";
import { ProjectHealthBadge } from "./project-health-badge.tsx";
import { ProjectTypeBadge } from "./project-type-badge.tsx";
import type { ProjectDetail } from "../types.ts";

export interface ProjectsHeaderProps {
  readonly project?: ProjectDetail | null;
  readonly onBack?: () => void;
  readonly onStart?: () => void;
  readonly onHold?: () => void;
  readonly onResume?: () => void;
  readonly onCompleteExecution?: () => void;
  readonly onCloseProject?: () => void;
  readonly onReopenProject?: () => void;
  readonly onEdit?: () => void;
  readonly isActionPending?: boolean;
}

export function ProjectsHeader({
  project,
  onBack,
  onStart,
  onHold,
  onResume,
  onCompleteExecution,
  onCloseProject,
  onReopenProject,
  onEdit,
  isActionPending = false
}: ProjectsHeaderProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  if (!project) {
    return (
      <Surface
        variant="card"
        padding="normal"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          borderBottom: "1px solid var(--color-border-default, #cbd5e1)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <div>
            <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {t("title")}
            </h1>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("subtitle")}
            </p>
          </div>
        </div>
      </Surface>
    );
  }

  const state = project.lifecycleState;

  return (
    <Surface
      variant="card"
      padding="normal"
      style={{
        background: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #cbd5e1)",
        borderRadius: "var(--radius-lg, 12px)",
        marginBottom: "1rem",
        boxShadow: "0 2px 4px rgba(0,0,0,0.04)"
      }}
    >
      <div style={{ display: "flex", flexWrap: "wrap", justifyContent: "space-between", alignItems: "flex-start", gap: "1rem" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          {onBack && (
            <button
              type="button"
              onClick={onBack}
              style={{
                alignSelf: "flex-start",
                background: "none",
                border: "none",
                padding: 0,
                color: "var(--color-brand-primary, #2563eb)",
                fontSize: "0.875rem",
                fontWeight: 600,
                cursor: "pointer",
                display: "inline-flex",
                alignItems: "center",
                gap: "0.25rem"
              }}
            >
              <span>←</span> {t("actions.backToList")}
            </button>
          )}

          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            <span
              style={{
                fontFamily: "monospace",
                fontWeight: 700,
                fontSize: "1.125rem",
                padding: "0.2rem 0.5rem",
                background: "var(--color-surface-subtle, #f1f5f9)",
                borderRadius: "var(--radius-sm, 6px)",
                color: "var(--color-content-primary, #0f172a)",
                border: "1px solid var(--color-border-default, #cbd5e1)"
              }}
            >
              {project.projectNumber}
            </span>
            <h1 style={{ margin: 0, fontSize: "1.375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {project.name}
            </h1>
            <ProjectTypeBadge type={project.projectType} />
            <ProjectStatusBadge state={project.lifecycleState} />
            <ProjectHealthBadge health={project.executionHealth} />
          </div>

          <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
            {project.startDate && (
              <span>
                <strong>{t("fields.startDate")}:</strong> {project.startDate}
              </span>
            )}
            {project.targetEndDate && (
              <span>
                <strong>{t("fields.targetEndDate")}:</strong> {project.targetEndDate}
              </span>
            )}
            {project.billingModel && (
              <span>
                <strong>{t("fields.billingModel")}:</strong> {t(`billingModels.${project.billingModel}` as const)}
              </span>
            )}
            {project.currencyCode && (
              <span>
                <strong>{t("fields.currencyCode")}:</strong> {project.currencyCode}
              </span>
            )}
          </div>
        </div>

        {/* Action Buttons for Lifecycle Transitions */}
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
          {onEdit && (
            <Button variant="secondary" size="compact" onClick={onEdit} isDisabled={isActionPending}>
              {t("actions.edit")}
            </Button>
          )}

          {state === "Draft" && onStart && (
            <Button variant="primary" size="compact" onClick={onStart} isDisabled={isActionPending}>
              {t("actions.startProject")}
            </Button>
          )}

          {state === "Active" && onHold && (
            <Button variant="secondary" size="compact" onClick={onHold} isDisabled={isActionPending}>
              {t("actions.holdProject")}
            </Button>
          )}

          {state === "OnHold" && onResume && (
            <Button variant="primary" size="compact" onClick={onResume} isDisabled={isActionPending}>
              {t("actions.resumeProject")}
            </Button>
          )}

          {state === "Active" && onCompleteExecution && (
            <Button variant="secondary" size="compact" onClick={onCompleteExecution} isDisabled={isActionPending}>
              {t("actions.completeExecution")}
            </Button>
          )}

          {state === "ExecutionCompleted" && onCloseProject && (
            <Button variant="primary" size="compact" onClick={onCloseProject} isDisabled={isActionPending}>
              {t("actions.closeProject")}
            </Button>
          )}

          {state === "Closed" && onReopenProject && (
            <Button variant="secondary" size="compact" onClick={onReopenProject} isDisabled={isActionPending}>
              {t("actions.reopenProject")}
            </Button>
          )}
        </div>
      </div>
    </Surface>
  );
}
