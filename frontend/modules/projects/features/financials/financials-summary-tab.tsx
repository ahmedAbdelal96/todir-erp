import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ProjectMoney } from "../../components/project-money.tsx";
import { ProjectsKpiCard } from "../../components/projects-kpi-card.tsx";
import {
  CalculatorBudgetIcon,
  ReceiptBillingIcon,
  PlusCircleIcon
} from "../../components/projects-icons.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { FinancialSummary, ProjectDetail } from "../../types.ts";

export interface FinancialsSummaryTabProps {
  readonly project: ProjectDetail;
  readonly summary?: FinancialSummary | null | undefined;
  readonly isLoading: boolean;
  readonly onCreateBudget: () => void;
  readonly onReviseBudget: () => void;
  readonly onCreateForecast: () => void;
  readonly onRecordCostFact: () => void;
  readonly onRecordCommitment: () => void;
}

export function FinancialsSummaryTab({
  project,
  summary,
  isLoading,
  onCreateBudget,
  onReviseBudget,
  onCreateForecast,
  onRecordCostFact,
  onRecordCommitment
}: FinancialsSummaryTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const isMobile = useIsMobile();

  const currency = summary?.currencyCode || project.currencyCode || "EGP";
  const budget = summary?.budget ?? project.budget;
  const actualCost = summary?.actualCost ?? project.actualCost;
  const openCommitment = summary?.openCommitment ?? project.openCommitment;
  const etc = summary?.etc ?? project.etc;
  const eac = summary?.eac ?? project.eac;
  const expectedMargin = summary?.expectedMargin ?? 0;
  const revenueBasis = summary?.revenueBasis ?? project.billingModel;
  const revenueBasisAmount = summary?.revenueBasisAmount ?? 0;

  // Margin tone
  const marginTone = expectedMargin > 0 ? "success" : expectedMargin < 0 ? "danger" : "default";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Financial Action Bar */}
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
          <CalculatorBudgetIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.financials")}
          </h2>
        </div>

        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="secondary" size="compact" onClick={onRecordCostFact} data-testid="btn-record-cost">
            {t("actions.recordCost")}
          </Button>
          <Button variant="secondary" size="compact" onClick={onRecordCommitment} data-testid="btn-record-po">
            {t("actions.recordCommitment")}
          </Button>
          <Button variant="secondary" size="compact" onClick={onCreateForecast} data-testid="btn-create-forecast">
            {t("actions.createForecast")}
          </Button>
          {budget > 0 ? (
            <Button variant="primary" size="compact" onClick={onReviseBudget} data-testid="btn-revise-budget">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.reviseBudget")}
            </Button>
          ) : (
            <Button variant="primary" size="compact" onClick={onCreateBudget} data-testid="btn-create-budget">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.createBudget")}
            </Button>
          )}
        </div>
      </Surface>

      {/* 360 Financial KPI Grid */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: isMobile ? "1fr" : "repeat(3, 1fr)",
          gap: "1rem"
        }}
      >
        {/* Cost & Commitments Column */}
        <ProjectsKpiCard
          title={t("kpis.totalBudget")}
          value={<ProjectMoney amount={budget} currency={currency} />}
          subtitle={t("fields.initialBudget")}
          tone="primary"
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-budget"
        />
        <ProjectsKpiCard
          title={t("kpis.actualCost")}
          value={<ProjectMoney amount={actualCost} currency={currency} />}
          subtitle={t("help.financialsHelp")}
          tone={actualCost > budget && budget > 0 ? "danger" : "default"}
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-actual-cost"
        />
        <ProjectsKpiCard
          title={t("kpis.openCommitments")}
          value={<ProjectMoney amount={openCommitment} currency={currency} />}
          subtitle={t("help.financialsHelp")}
          tone="warning"
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-open-commitments"
        />

        {/* ETC & EAC Forecast Column */}
        <ProjectsKpiCard
          title={t("kpis.etc")}
          value={<ProjectMoney amount={etc} currency={currency} />}
          subtitle={t("fields.etcAmount")}
          tone="default"
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-etc"
        />
        <ProjectsKpiCard
          title={t("kpis.eac")}
          value={<ProjectMoney amount={eac} currency={currency} />}
          subtitle={t("fields.eacAmount")}
          tone={eac > budget && budget > 0 ? "danger" : "success"}
          icon={<CalculatorBudgetIcon size={20} />}
          data-testid="kpi-eac"
        />
        <ProjectsKpiCard
          title={t("kpis.expectedMargin")}
          value={<ProjectMoney amount={expectedMargin} currency={currency} />}
          subtitle={t("kpis.revenueBasis")}
          tone={marginTone}
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-expected-margin"
        />
      </div>

      {/* Explanatory Explanations & Policy Card */}
      <Surface
        variant="card"
        style={{
          padding: "1.25rem 1.5rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
          {t("help.financialsHelp")}
        </h3>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.6 }}>
          {t("help.overviewHelp")}
        </p>
      </Surface>
    </div>
  );
}
