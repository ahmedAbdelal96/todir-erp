import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ProjectMoney } from "../../components/project-money.tsx";
import { ProjectsKpiCard } from "../../components/projects-kpi-card.tsx";
import { ProjectStatusBadge } from "../../components/project-status-badge.tsx";
import { ProjectHealthBadge } from "../../components/project-health-badge.tsx";
import { ProjectTypeBadge } from "../../components/project-type-badge.tsx";
import {
  ProjectBriefcaseIcon,
  CalculatorBudgetIcon,
  ReceiptBillingIcon,
  ClockTimeIcon,
  PlusCircleIcon
} from "../../components/projects-icons.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { ProjectListItem, ProjectPortfolioItem } from "../../types.ts";

export interface ProjectPortfolioTabProps {
  readonly projects: readonly ProjectListItem[];
  readonly portfolioItems?: readonly ProjectPortfolioItem[] | undefined;
  readonly isLoading: boolean;
  readonly onSelectProject: (projectId: string) => void;
  readonly onNewProject: () => void;
  readonly onFromSalesSource: () => void;
  readonly search: string;
  readonly onSearchChange: (search: string) => void;
  readonly stateFilter: string;
  readonly onStateFilterChange: (state: string) => void;
}

export function ProjectPortfolioTab({
  projects,
  portfolioItems,
  isLoading,
  onSelectProject,
  onNewProject,
  onFromSalesSource,
  search,
  onSearchChange,
  stateFilter,
  onStateFilterChange
}: ProjectPortfolioTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const isMobile = useIsMobile();
  const [typeFilter, setTypeFilter] = useState<string>("ALL");

  const filteredProjects = projects.filter((p) => {
    if (typeFilter !== "ALL" && p.projectType !== typeFilter) return false;
    return true;
  });

  const totalProjects = projects.length;
  const activeProjects = projects.filter((p) => p.lifecycleState === "Active").length;
  const onTrackProjects = projects.filter((p) => p.executionHealth === "OnTrack").length;
  const atRiskProjects = projects.filter((p) => p.executionHealth === "AtRisk" || p.executionHealth === "OffTrack").length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top KPI Cards Grid */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: isMobile ? "1fr 1fr" : "repeat(4, 1fr)",
          gap: "1rem"
        }}
      >
        <ProjectsKpiCard
          title={t("kpis.displayedProjects")}
          value={totalProjects}
          subtitle={`${activeProjects} ${t("states.Active")}`}
          tone="primary"
          icon={<ProjectBriefcaseIcon size={20} />}
          data-testid="kpi-total-projects"
        />
        <ProjectsKpiCard
          title={t("states.Active")}
          value={activeProjects}
          subtitle={`${onTrackProjects} ${t("health.OnTrack")}`}
          tone="success"
          icon={<ClockTimeIcon size={20} />}
          data-testid="kpi-active-projects"
        />
        <ProjectsKpiCard
          title={t("health.AtRisk")}
          value={atRiskProjects}
          subtitle={atRiskProjects > 0 ? t("kpis.operationalFollowupRequired") : t("kpis.allProjectsStable")}
          tone={atRiskProjects > 0 ? "warning" : "default"}
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-atrisk-projects"
        />
        <ProjectsKpiCard
          title={t("kpis.portfolioBudget")}
          value="—"
          subtitle={t("kpis.portfolioWideTotalsUnavailable")}
          tone="default"
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-portfolio-budget"
        />
      </div>

      {/* Filter Bar */}
      <Surface
        variant="card"
        style={{
          padding: "1rem 1.25rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "space-between",
          alignItems: "center",
          gap: "0.75rem"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap", flex: 1 }}>
          <input
            type="search"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={t("actions.search")}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              minWidth: isMobile ? "100%" : "18rem",
              fontSize: "0.875rem"
            }}
          />

          <select
            value={stateFilter}
            onChange={(e) => onStateFilterChange(e.target.value)}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem",
              background: "#ffffff"
            }}
          >
            <option value="">{t("actions.filter")} - {t("fields.lifecycleState")}</option>
            <option value="Active">{t("states.Active")}</option>
            <option value="Draft">{t("states.Draft")}</option>
            <option value="OnHold">{t("states.OnHold")}</option>
            <option value="ExecutionCompleted">{t("states.ExecutionCompleted")}</option>
            <option value="Closed">{t("states.Closed")}</option>
            <option value="Cancelled">{t("states.Cancelled")}</option>
          </select>

          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem",
              background: "#ffffff"
            }}
          >
            <option value="ALL">{t("actions.filter")} - {t("fields.projectType")}</option>
            <option value="CustomerProject">{t("types.CustomerProject")}</option>
            <option value="InternalProject">{t("types.InternalProject")}</option>
            <option value="CapitalProject">{t("types.CapitalProject")}</option>
          </select>
        </div>
      </Surface>

      {/* Projects List / Table */}
      <Surface
        variant="card"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflow: "hidden",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("actions.refresh")}...
          </div>
        ) : filteredProjects.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("messages.noProjectsFound")}
          </div>
        ) : isMobile ? (
          /* Mobile Card View */
          <div style={{ display: "flex", flexDirection: "column" }}>
            {filteredProjects.map((p) => (
              <div
                key={p.id}
                onClick={() => onSelectProject(p.id)}
                style={{
                  padding: "1rem",
                  borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                  cursor: "pointer",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.5rem"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontFamily: "monospace", fontWeight: 700, fontSize: "0.875rem", color: "var(--color-brand-primary, #2563eb)" }}>
                    {p.projectNumber}
                  </span>
                  <div style={{ display: "flex", gap: "0.35rem" }}>
                    <ProjectStatusBadge state={p.lifecycleState} size="small" />
                    <ProjectHealthBadge health={p.executionHealth} size="small" />
                  </div>
                </div>
                <div style={{ fontWeight: 600, fontSize: "1rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {p.name}
                </div>
                <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                  {p.startDate}{p.targetEndDate ? ` — ${p.targetEndDate}` : ""}
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "0.25rem" }}>
                  <ProjectTypeBadge type={p.projectType} size="small" />
                  <Button
                    variant="secondary"
                    size="compact"
                    onClick={(e) => {
                      e.stopPropagation();
                      onSelectProject(p.id);
                    }}
                  >
                    {t("actions.details")}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          /* Desktop Table View */
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.projectNumber")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.name")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.projectType")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.lifecycleState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.executionHealth")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.billingModel")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.dates")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredProjects.map((p) => (
                  <tr
                    key={p.id}
                    onClick={() => onSelectProject(p.id)}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                      cursor: "pointer",
                      transition: "background 0.15s ease"
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f1f5f9")}
                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontFamily: "monospace", fontWeight: 700, color: "var(--color-brand-primary, #2563eb)" }}>
                      {p.projectNumber}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {p.name}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <ProjectTypeBadge type={p.projectType} size="small" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <ProjectStatusBadge state={p.lifecycleState} size="small" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <ProjectHealthBadge health={p.executionHealth} size="small" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                      {t(`billingModels.${p.billingModel}` as const)}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                      {p.startDate}{p.targetEndDate ? ` — ${p.targetEndDate}` : ""}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={(e: React.MouseEvent) => {
                          e.stopPropagation();
                          onSelectProject(p.id);
                        }}
                      >
                        {t("actions.details")}
                      </Button>
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
