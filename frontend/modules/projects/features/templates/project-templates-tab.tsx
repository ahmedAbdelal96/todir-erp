import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { DocumentFolderIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import { ProjectTypeBadge } from "../../components/project-type-badge.tsx";
import type { ProjectTemplateView, ProjectType } from "../../types.ts";

export interface ProjectTemplatesTabProps {
  readonly templates: readonly ProjectTemplateView[];
  readonly isLoading: boolean;
  readonly onNewTemplate: () => void;
}

export function ProjectTemplatesTab({
  templates,
  isLoading,
  onNewTemplate
}: ProjectTemplatesTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");

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
          <DocumentFolderIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.templates")}
          </h2>
        </div>

        <Button variant="primary" size="compact" onClick={onNewTemplate} data-testid="btn-new-template">
          <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
          {t("actions.newTemplate")}
        </Button>
      </Surface>

      {/* Templates Table */}
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
        ) : templates.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("messages.noTemplates")}
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.code")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.name")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.projectType")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.version")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                </tr>
              </thead>
              <tbody>
                {templates.map((tpl) => (
                  <tr key={tpl.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontFamily: "monospace", fontWeight: 700, color: "var(--color-brand-primary, #2563eb)" }}>
                      {tpl.code}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {tpl.name}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <ProjectTypeBadge type={tpl.projectType as ProjectType} size="small" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                      {tpl.version}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span style={{ fontWeight: 600, color: "var(--color-status-success, #059669)" }}>{tpl.status}</span>
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
