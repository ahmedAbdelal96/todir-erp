import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useDisqualifyLeadMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead } from "../../types.ts";

export interface DisqualifyLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onDisqualified?: () => void;
}

export function DisqualifyLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onDisqualified
}: DisqualifyLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const disqualifyMutation = useDisqualifyLeadMutation(legalEntityId, lead?.id || "");

  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead) return;
    if (!reason.trim()) {
      setErrorMessage(t("errors.disqualifyReasonRequired"));
      return;
    }

    try {
      await disqualifyMutation.mutateAsync({
        reason: reason.trim(),
        version: lead.version
      });

      setReason("");
      onClose();
      onDisqualified?.();
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
      title={t("leads.disqualifyModal.title")}
      size="small"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={disqualifyMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="disqualify-lead-form"
            isDisabled={disqualifyMutation.isPending}
          >
            {disqualifyMutation.isPending ? t("actions.saving") : t("leads.disqualifyModal.submit")}
          </Button>
        </>
      }
    >
      <form id="disqualify-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          {t("leads.disqualifyModal.leadName")}: <strong>{lead?.displayName}</strong>
        </div>

        <TextField
          label={t("leads.disqualifyModal.reason")}
          value={reason}
          onChange={setReason}
          isRequired
          placeholder={t("leads.disqualifyModal.reasonPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
