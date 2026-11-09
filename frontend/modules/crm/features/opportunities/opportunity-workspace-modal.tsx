import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { DealHealthBadge } from "../../components/deal-health-badge.tsx";
import { ForecastCategoryBadge } from "../../components/forecast-category-badge.tsx";
import { StageProgressBar } from "../../components/stage-progress-bar.tsx";
import { useOpportunity, usePipelines, useMoveOpportunityStageMutation } from "../../hooks/use-crm-queries.ts";
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
  const opportunityQuery = useOpportunity(legalEntityId, opportunity?.id || "");

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

  // List and home cards carry a snapshot. The workspace reads the authoritative
  // record so every mutation uses the latest optimistic-concurrency version.
  const currentOpportunity = opportunityQuery.data ?? opportunity;
  const activePipeline = pipelines.find((p) => p.id === currentOpportunity.pipelineId) || pipelines[0];
  const stages = activePipeline?.stages || [];

  const handleStageSelect = async (stageId: string) => {
    try {
      // Re-read immediately before the write. This closes the small window in
      // which a list snapshot or background refetch could leave the version
      // sent to the optimistic-concurrency endpoint stale.
      const latest = (await opportunityQuery.refetch()).data ?? currentOpportunity;
      await moveStageMutation.mutateAsync({
        stageId,
        version: latest.version
      });
      onUpdated?.();
      setErrorMessage(null);
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      if (mapped.code === "crm.opportunity_stale" || mapped.code === "crm.concurrency_conflict") {
        await opportunityQuery.refetch();
        onUpdated?.();
      }
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
        title={currentOpportunity.name}
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
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-brand-primary, #102a43)" }}>
                <MoneyDisplay
                  value={{ amount: String(currentOpportunity.estimatedAmount), currency: currentOpportunity.currencyCode || "EGP" }}
                  size="large"
                />
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <DealHealthBadge status={currentOpportunity.healthStatus} score={currentOpportunity.healthScore} />
              <ForecastCategoryBadge category={currentOpportunity.forecastCategory} />
              <span
                style={{
                  padding: "0.1875rem 0.5rem",
                  borderRadius: "var(--radius-sm)",
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  backgroundColor:
                    currentOpportunity.status === "Won" ? "var(--color-status-success-subtle)" :
                    currentOpportunity.status === "Lost" ? "var(--color-status-danger-subtle)" :
                    "var(--color-surface-card)",
                  color:
                    currentOpportunity.status === "Won" ? "var(--color-status-success)" :
                    currentOpportunity.status === "Lost" ? "var(--color-status-danger)" :
                    "var(--color-content-primary)"
                }}
              >
                {t(`opportunityStatuses.${currentOpportunity.status}`)}
              </span>
            </div>
          </div>

          {/* Interactive Pipeline Stage Advancement */}
          <div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.375rem" }}>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                {t("opportunities.stageTrack")}: {activePipeline?.name || currentOpportunity.pipelineName}
              </span>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {t("opportunities.clickStageToMove")}
              </span>
            </div>
            <StageProgressBar
              stages={stages}
              currentStageId={currentOpportunity.stageId}
              onSelectStage={currentOpportunity.status === "Open" ? handleStageSelect : undefined}
              isReadOnly={currentOpportunity.status !== "Open" || opportunityQuery.isFetching || moveStageMutation.isPending}
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

            {currentOpportunity.status === "Open" && (
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
                {currentOpportunity.expectedCloseDate ? <LocalDateField value={currentOpportunity.expectedCloseDate} /> : "—"}
              </span>
            </div>

            <div style={{ padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-card)" }}>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.createdAt")}
              </span>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                <LocalDateField value={currentOpportunity.createdAt} />
              </span>
            </div>

            <div style={{ padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-card)" }}>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("opportunities.fields.currentStage")}
              </span>
              <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                {currentOpportunity.stageName}
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
        opportunity={currentOpportunity}
        onUpdated={onUpdated}
      />

      <AssignOpportunityModal
        isOpen={isAssignOpen}
        onClose={() => setIsAssignOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
        onAssigned={onUpdated}
      />

      <WinOpportunityModal
        isOpen={isWinOpen}
        onClose={() => setIsWinOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
        onWon={() => {
          onUpdated?.();
          onClose();
        }}
      />

      <LoseOpportunityModal
        isOpen={isLoseOpen}
        onClose={() => setIsLoseOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
        onLost={() => {
          onUpdated?.();
          onClose();
        }}
      />

      <QuoteHandoffModal
        isOpen={isQuoteOpen}
        onClose={() => setIsQuoteOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
      />

      <OpportunityInterestsModal
        isOpen={isInterestsOpen}
        onClose={() => setIsInterestsOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
      />

      <BuyingCommitteeModal
        isOpen={isCommitteeOpen}
        onClose={() => setIsCommitteeOpen(false)}
        legalEntityId={legalEntityId}
        opportunity={currentOpportunity}
      />
    </>
  );
}
