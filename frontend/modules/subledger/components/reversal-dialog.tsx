import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { useReverseApplicationMutation } from "../hooks/subledger-queries.ts";

export interface ReversalDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly applicationId: string;
  readonly onSuccess?: () => void;
  readonly onCompleted?: () => void;
}

export function ReversalDialog({
  isOpen,
  onClose,
  legalEntityId,
  applicationId,
  onSuccess,
  onCompleted
}: ReversalDialogProps): React.JSX.Element | null {
  const { t } = useTranslation("subledger");
  const [reason, setReason] = useState("");
  const [effectiveDate, setEffectiveDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const reverseMutation = useReverseApplicationMutation();

  const handleConfirm = async () => {
    if (!reason.trim()) {
      setError(t("reversal.reasonLabel"));
      return;
    }
    setError(null);

    try {
      await reverseMutation.mutateAsync({
        legalEntityId,
        applicationId,
        idempotencyKey: crypto.randomUUID(),
        effectiveDate,
        reason: reason.trim()
      });
      if (onSuccess) onSuccess();
      if (onCompleted) onCompleted();
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="medium"
      title={t("reversal.title")}
      description={t("reversal.applicationId") + ": " + applicationId}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", width: "100%" }}>
          <Button variant="secondary" onClick={onClose} isDisabled={reverseMutation.isPending}>
            {t("reversal.cancelButton")}
          </Button>
          <Button
            variant="danger"
            onClick={() => void handleConfirm()}
            isLoading={reverseMutation.isPending}
            isDisabled={!reason.trim()}
          >
            {t("reversal.confirmButton")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
        <Alert variant="warning" title={t("reversal.warning")}>
          {t("reversal.warning")}
        </Alert>

        {error && <Alert variant="danger">{error}</Alert>}

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("reversal.effectiveDate")}
          </label>
          <input
            type="date"
            value={effectiveDate}
            onChange={(e) => setEffectiveDate(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem"
            }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("reversal.reasonLabel")} *
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("reversal.reasonPlaceholder")}
            rows={3}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem",
              fontFamily: "inherit"
            }}
          />
        </div>
      </div>
    </Dialog>
  );
}
