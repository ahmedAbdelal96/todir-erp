import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useWinOpportunityMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";

export interface WinOpportunityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onWon?: () => void;
}

export function WinOpportunityModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onWon
}: WinOpportunityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const winMutation = useWinOpportunityMutation(legalEntityId, opportunity?.id || "");

  const [notes, setNotes] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!opportunity) return;

    try {
      await winMutation.mutateAsync({
        version: opportunity.version,
        reason: notes.trim() || null
      });

      setNotes("");
      onClose();
      onWon?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.winModal.title")}
      size="small"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={winMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="win-opp-form"
            isDisabled={winMutation.isPending}
            style={{ backgroundColor: "var(--color-status-success)", borderColor: "var(--color-status-success)" }}
          >
            {winMutation.isPending ? t("actions.saving") : t("opportunities.winModal.submit")}
          </Button>
        </>
      }
    >
      <form id="win-opp-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          {t("opportunities.winModal.message", { name: opportunity?.name })}
        </div>

        <TextField
          label={t("opportunities.winModal.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("opportunities.winModal.notesPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
