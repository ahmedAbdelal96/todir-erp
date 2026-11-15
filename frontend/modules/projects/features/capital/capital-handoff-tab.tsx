import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { CapitalAssetIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import { ProjectMoney } from "../../components/project-money.tsx";
import type { ProjectDetail } from "../../types.ts";

export interface CapitalHandoffTabProps {
  readonly project: ProjectDetail;
  readonly isLoading: boolean;
  readonly onCapitalHandoff: () => void;
}

export function CapitalHandoffTab({
  project,
  isLoading,
  onCapitalHandoff
}: CapitalHandoffTabProps): React.JSX.Element {
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
          <CapitalAssetIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.capital")}
          </h2>
        </div>

        <Button variant="primary" size="compact" onClick={onCapitalHandoff} data-testid="btn-capital-handoff">
          <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
          {t("actions.capitalHandoff")}
        </Button>
      </Surface>

      {/* Capital Projects Explanation & Status Card */}
      <Surface
        variant="card"
        style={{
          padding: "1.5rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 0.5rem", fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
          {t("help.capitalHelp")}
        </h3>
        <p style={{ margin: "0 0 1rem", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.6 }}>
          {t("help.overviewHelp")}
        </p>

        <div style={{ background: "var(--color-surface-subtle, #f8fafc)", padding: "1rem", borderRadius: "var(--radius-md, 8px)", border: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
          <div style={{ fontWeight: 600, fontSize: "0.875rem", marginBottom: "0.25rem" }}>
            {t("fields.projectType")}: {t(`types.${project.projectType}` as const)}
          </div>
          <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", display: "flex", gap: "0.5rem", alignItems: "center" }}>
            <span>{t("kpis.actualCost")}:</span>
            <ProjectMoney amount={project.actualCost} currency={project.currencyCode} />
          </div>
        </div>
      </Surface>
    </div>
  );
}
