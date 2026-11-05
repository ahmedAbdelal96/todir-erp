import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { DealHealthBadge } from "../../components/deal-health-badge.tsx";
import { ForecastCategoryBadge } from "../../components/forecast-category-badge.tsx";
import { StageProgressBar } from "../../components/stage-progress-bar.tsx";
import { usePipelines, useMoveOpportunityStageMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";
import { EditOpportunityModal } from "./edit-opportunity-modal.tsx";
import { AssignOpportunityModal } from "./assign-opportunity-modal.tsx";
import { WinOpportunityModal } from "./win-opportunity-modal.tsx";
import { LoseOpportunityModal } from "./lose-opportunity-modal.tsx";
import { QuoteHandoffModal } from "./quote-handoff-modal.tsx";
import { OpportunityInterestsModal } from "./opportunity-interests-modal.tsx";
import { BuyingCommitteeModal } from "./buying-committee-modal.tsx";

export interface OpportunityWorkspaceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onUpdated?: () => void;
}

export function OpportunityWorkspaceModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onUpdated
}: OpportunityWorkspaceModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const { data: pipelines = [] } = usePipelines(legalEntityId);
  const moveStageMutation = useMoveOpportunityStageMutation(legalEntityId, opportunity?.id || "");

  // Sub modals
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [isAssignOpen, setIsAssignOpen] = useState(false);
  const [isWinOpen, setIsWinOpen] = useState(false);
  const [isLoseOpen, setIsLoseOpen] = useState(false);
  const [isQuoteOpen, setIsQuoteOpen] = useState(false);
  const [isInterestsOpen, setIsInterestsOpen] = useState(false);
  const [isCommitteeOpen, setIsCommitteeOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!opportunity) {
    return <></>;
  }

  const activePipeline = pipelines.find((p) => p.id === opportunity.pipelineId) || pipelines[0];
  const stages = activePipeline?.stages || [];

  const handleStageSelect = async (stageId: string) => {
    try {
      await moveStageMutation.mutateAsync({
        stageId,
        version: opportunity.version
      });
      onUpdated?.();
      setErrorMessage(null);
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <>
      <Dialog
        isOpen={isOpen}
        onOpenChange={(open: boolean) => {
          if (!open) onClose();
        }}
        title={opportunity.name}
        size="large"
        footer={
          <Button variant="secondary" onPress={onClose}>
            {t("actions.close")}
          </Button>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {errorMessage && (
            <div
              role="alert"
              style={{
                padding: "0.625rem 0.875rem",
                borderRadius: "var(--radius-sm)",
                background: "var(--color-status-danger-subtle, #fee2e2)",
                color: "var(--color-status-danger, #b91c1c)",
                fontSize: "0.8125rem"
              }}
            >
              {errorMessage}
            </div>
          )}

          {/* Deal Value & Status Ribbon */}
          <div
            style={{
              padding: "1rem 1.25rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: "0.75rem"
            }}
          >
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.estimatedAmount")}
              </span>
              <MoneyDisplay
                value={{ amount: String(opportunity.estimatedAmount), currency: opportunity.currencyCode || "EGP" }}
                size="large"
                className="font-black text-2xl text-sky-700"
              />
            </div>

            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <DealHealthBadge status={opportunity.healthStatus} score={opportunity.healthScore} />
              <ForecastCategoryBadge category={opportunity.forecastCategory} />
              <span
                style={{
                  padding: "0.1875rem 0.5rem",
                  borderRadius: "var(--radius-sm)",
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  backgroundColor:
                    opportunity.status === "Won" ? "var(--color-status-success-subtle)" :
                    opportunity.status === "Lost" ? "var(--color-status-danger-subtle)" :
                    "var(--color-surface-card)",
                  color:
                    opportunity.status === "Won" ? "var(--color-status-success)" :
                    opportunity.status === "Lost" ? "var(--color-status-danger)" :
                    "var(--color-content-primary)"
                }}
              >
                {t(`opportunityStatuses.${opportunity.status}`)}
              </span>
            </div>
          </div>

          {/* Interactive Pipeline Stage Advancement */}
          <div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.375rem" }}>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                {t("opportunities.stageTrack")}: {activePipeline?.name || opportunity.pipelineName}
              </span>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {t("opportunities.clickStageToMove")}
              </span>
            </div>
            <StageProgressBar
              stages={stages}
              currentStageId={opportunity.stageId}
              onSelectStage={opportunity.status === "Open" ? handleStageSelect : undefined}
              isReadOnly={opportunity.status !== "Open"}
            />
          </div>

          {/* Action Hub */}
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center", borderTop: "1px solid var(--color-border-default)", paddingTop: "0.875rem" }}>
            <Button
              variant="primary"
              size="comfortable"
              onPress={() => setIsQuoteOpen(true)}
            >
              📄 {t("opportunities.actions.createQuotation")}
            </Button>

            {opportunity.status === "Open" && (
              <>
                <Button
                  variant="secondary"
                  size="comfortable"
                  onPress={() => setIsWinOpen(true)}
                  style={{ color: "var(--color-status-success)" }}
                >
                  🏆 {t("opportunities.markWon")}
                </Button>
                <Button
                  variant="secondary"
                  size="comfortable"
                  onPress={() => setIsLoseOpen(true)}
                  style={{ color: "var(--color-status-danger)" }}
                >
                  ✕ {t("opportunities.markLost")}
                </Button>
              </>
            )}

            <Button
              variant="secondary"
              size="comfortable"
              onPress={() => setIsInterestsOpen(true)}
            >
              📦 {t("opportunities.actions.interests")}
            </Button>

            <Button
              variant="secondary"
              size="comfortable"
              onPress={() => setIsCommitteeOpen(true)}
            >
              👥 {t("opportunities.actions.committee")}
            </Button>

            <Button
              variant="secondary"
              size="comfortable"
              onPress={() => setIsAssignOpen(true)}
            >
              {t("opportunities.actions.assign")}
            </Button>

            <Button
              variant="secondary"
              size="comfortable"
              onPress={() => setIsEditOpen(true)}
            >
              {t("actions.edit")}
            </Button>
          </div>

          {/* Commercial Details Cards Grid */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
            <div style={{ padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-card)" }}>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.expectedCloseDate")}
              </span>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                {opportunity.expectedCloseDate ? <LocalDateField value={opportunity.expectedCloseDate} /> : "—"}
              </span>
            </div>

            <div style={{ padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-card)" }}>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.createdAt")}
              </span>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                <LocalDateField value={opportunity.createdAt} />
              </span>
            </div>

            <div style={{ padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-card)" }}>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.currentStage")}
              </span>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                {opportunity.stageName}
              </span>
            </div>
          </div>
        </div>
      </Dialog>

      {/* Sub modals */}
      <EditOpportunityModal
        isOpen={isEditOpen}
        onClose={() => setIsEditOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
        onUpdated={onUpdated}
      />

      <AssignOpportunityModal
        isOpen={isAssignOpen}
        onClose={() => setIsAssignOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
        onAssigned={onUpdated}
      />

      <WinOpportunityModal
        isOpen={isWinOpen}
        onClose={() => setIsWinOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
        onWon={() => {
          onUpdated?.();
          onClose();
        }}
      />

      <LoseOpportunityModal
        isOpen={isLoseOpen}
        onClose={() => setIsLoseOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
        onLost={() => {
          onUpdated?.();
          onClose();
        }}
      />

      <QuoteHandoffModal
        isOpen={isQuoteOpen}
        onClose={() => setIsQuoteOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
      />

      <OpportunityInterestsModal
        isOpen={isInterestsOpen}
        onClose={() => setIsInterestsOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
      />

      <BuyingCommitteeModal
        isOpen={isCommitteeOpen}
        onClose={() => setIsCommitteeOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={opportunity}
      />
    </>
  );
}
