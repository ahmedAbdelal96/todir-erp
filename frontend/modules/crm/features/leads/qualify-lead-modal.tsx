import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useQualifyLeadMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead } from "../../types.ts";

export interface QualifyLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onQualified?: () => void;
}

export function QualifyLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onQualified
}: QualifyLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const qualifyMutation = useQualifyLeadMutation(legalEntityId, lead?.id || "");

  const [need, setNeed] = useState("");
  const [budget, setBudget] = useState("");
  const [timing, setTiming] = useState("");
  const [decisionRole, setDecisionRole] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (lead) {
      setNeed("");
      setBudget("");
      setTiming("");
      setDecisionRole("");
      setErrorMessage(null);
    }
  }, [lead]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead) return;

    try {
      await qualifyMutation.mutateAsync({
        partyId: lead.partyId || null,
        need: need.trim() || null,
        budget: budget.trim() || null,
        timing: timing.trim() || null,
        decisionRole: decisionRole.trim() || null,
        version: lead.version
      });

      onClose();
      onQualified?.();
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
      title={t("leads.qualifyModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={qualifyMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="qualify-lead-form"
            isDisabled={qualifyMutation.isPending}
          >
            {qualifyMutation.isPending ? t("actions.saving") : t("leads.qualifyModal.submit")}
          </Button>
        </>
      }
    >
      <form id="qualify-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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
          {t("leads.qualifyModal.leadName")}: <strong>{lead?.displayName}</strong>
          {lead?.companyName && ` — ${lead.companyName}`}
        </div>

        <TextField
          label={t("leads.qualifyModal.need")}
          value={need}
          onChange={setNeed}
          placeholder={t("leads.qualifyModal.needPlaceholder")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("leads.qualifyModal.budget")}
            value={budget}
            onChange={setBudget}
            placeholder={t("leads.qualifyModal.budgetPlaceholder")}
          />
          <TextField
            label={t("leads.qualifyModal.timing")}
            value={timing}
            onChange={setTiming}
            placeholder={t("leads.qualifyModal.timingPlaceholder")}
          />
        </div>

        <TextField
          label={t("leads.qualifyModal.decisionRole")}
          value={decisionRole}
          onChange={setDecisionRole}
          placeholder={t("leads.qualifyModal.decisionRolePlaceholder")}
        />
      </form>
    </Dialog>
  );
}
