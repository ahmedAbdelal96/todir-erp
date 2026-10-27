import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";

export interface CancelDraftDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onConfirm: (reason: string) => Promise<void>;
  readonly isCancelling?: boolean | undefined;
}

export function CancelDraftDialog({
  isOpen,
  onClose,
  onConfirm,
  isCancelling = false
}: CancelDraftDialogProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return <></>;

  const handleConfirm = async () => {
    if (!reason.trim()) {
      setError(t("draft.cancelDraftReasonRequired"));
      return;
    }
    setError(null);
    await onConfirm(reason);
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={t("draft.cancelDraft")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary, #475569)" }}>
          {t("draft.cancelDraftConfirm")}
        </p>

        {error && (
          <div style={{ fontSize: "0.8125rem", color: "#dc2626", fontWeight: 600 }}>
            {error}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
            {t("draft.cancelDraftReasonLabel")} <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <TextField
            value={reason}
            onChange={setReason}
            placeholder={t("draft.cancelDraftReasonPlaceholder")}
            aria-label={t("draft.cancelDraftReasonLabel")}
            isRequired
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid #e2e8f0", paddingTop: "0.75rem" }}>
          <Button variant="secondary" onPress={onClose} isDisabled={isCancelling}>
            {t("correction.cancel")}
          </Button>
          <Button variant="danger" onPress={handleConfirm} isDisabled={isCancelling}>
            {isCancelling ? t("draft.cancellingDraft") : t("draft.cancelDraft")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
