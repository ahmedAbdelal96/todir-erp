import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useLoseOpportunityMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";

export interface LoseOpportunityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onLost?: () => void;
}

export function LoseOpportunityModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onLost
}: LoseOpportunityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const loseMutation = useLoseOpportunityMutation(legalEntityId, opportunity?.id || "");

  const [reason, setReason] = useState("Price");
  const [competitor, setCompetitor] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!opportunity) return;

    try {
      await loseMutation.mutateAsync({
        version: opportunity.version,
        reason: reason || "Price",
        competitor: competitor.trim() || null
      });

      setCompetitor("");
      onClose();
      onLost?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const reasonOptions = [
    { id: "Price", label: t("lossReasons.Price") },
    { id: "Competitor", label: t("lossReasons.Competitor") },
    { id: "NoDecision", label: t("lossReasons.NoDecision") },
    { id: "FeatureGap", label: t("lossReasons.FeatureGap") },
    { id: "Timing", label: t("lossReasons.Timing") },
    { id: "Other", label: t("lossReasons.Other") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.loseModal.title")}
      size="small"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={loseMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="lose-opp-form"
            isDisabled={loseMutation.isPending}
            style={{ backgroundColor: "var(--color-status-danger)", borderColor: "var(--color-status-danger)" }}
          >
            {loseMutation.isPending ? t("actions.saving") : t("opportunities.loseModal.submit")}
          </Button>
        </>
      }
    >
      <form id="lose-opp-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("opportunities.loseModal.message", { name: opportunity?.name })}
        </div>

        <Select
          label={t("opportunities.loseModal.reason")}
          selectedKey={reason}
          onSelectionChange={(k) => setReason(String(k))}
          options={reasonOptions}
        />

        <TextField
          label={t("opportunities.loseModal.competitor")}
          value={competitor}
          onChange={setCompetitor}
          placeholder={t("opportunities.loseModal.competitorPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
