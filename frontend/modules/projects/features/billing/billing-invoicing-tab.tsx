import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ProjectMoney } from "../../components/project-money.tsx";
import { ProjectsKpiCard } from "../../components/projects-kpi-card.tsx";
import { ReceiptBillingIcon, PlusCircleIcon, ArrowPathIcon } from "../../components/projects-icons.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import type { ProjectDetail } from "../../types.ts";

export interface BillingInvoicingTabProps {
  readonly project: ProjectDetail;
  readonly isLoading: boolean;
  readonly onNewBillingRule: () => void;
  readonly onAddPlanLine: () => void;
  readonly onGenerateCharges: () => void;
  readonly onCreateCharge: () => void;
  readonly onRequestInvoice: () => void;
  readonly isGeneratingCharges?: boolean;
}

export function BillingInvoicingTab({
  project,
  isLoading,
  onNewBillingRule,
  onAddPlanLine,
  onGenerateCharges,
  onCreateCharge,
  onRequestInvoice,
  isGeneratingCharges = false
}: BillingInvoicingTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const isMobile = useIsMobile();
  const currency = project.currencyCode || "EGP";

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
          <ReceiptBillingIcon size={18} color="var(--color-brand-primary, #102a43)" />
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.billing")}
          </h2>
        </div>

        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="secondary" size="compact" onClick={onNewBillingRule} data-testid="btn-new-rule">
            {t("actions.createBillingRule")}
          </Button>
          <Button variant="secondary" size="compact" onClick={onAddPlanLine} data-testid="btn-add-plan">
            {t("actions.addBillingPlanLine")}
          </Button>
          <Button
            variant="secondary"
            size="compact"
            onClick={onGenerateCharges}
            isDisabled={isGeneratingCharges}
            data-testid="btn-generate-charges"
          >
            <ArrowPathIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
            {t("actions.generateCharges")}
          </Button>
          <Button variant="secondary" size="compact" onClick={onCreateCharge} data-testid="btn-create-charge">
            {t("actions.createCharge")}
          </Button>
          <Button
            variant="primary"
            size="compact"
            onClick={onRequestInvoice}
            isDisabled={project.billingEligible <= 0}
            data-testid="btn-request-invoice"
          >
            <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
            {t("actions.requestInvoice")}
          </Button>
        </div>
      </Surface>

      {/* KPI Cards */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: isMobile ? "1fr 1fr" : "repeat(4, 1fr)",
          gap: "1rem"
        }}
      >
        <ProjectsKpiCard
          title={t("kpis.billingEligible")}
          value={<ProjectMoney amount={project.billingEligible} currency={currency} />}
          subtitle={t("fields.requestedAmount")}
          tone={project.billingEligible > 0 ? "primary" : "default"}
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-billing-eligible"
        />
        <ProjectsKpiCard
          title={t("kpis.invoiced")}
          value={<ProjectMoney amount={project.invoiced} currency={currency} />}
          subtitle={t("chargeStates.Invoiced")}
          tone="default"
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-invoiced"
        />
        <ProjectsKpiCard
          title={t("kpis.collected")}
          value={<ProjectMoney amount={project.collected} currency={currency} />}
          subtitle={t("kpis.collected")}
          tone="success"
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-collected"
        />
        <ProjectsKpiCard
          title={t("kpis.outstanding")}
          value={<ProjectMoney amount={project.outstanding} currency={currency} />}
          subtitle={t("kpis.outstanding")}
          tone={project.outstanding > 0 ? "warning" : "default"}
          icon={<ReceiptBillingIcon size={20} />}
          data-testid="kpi-outstanding"
        />
      </div>

      {/* Guidance Section */}
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
          {t("help.billingHelp")}
        </h3>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.6 }}>
          {t("help.overviewHelp")}
        </p>
      </Surface>
    </div>
  );
}
