import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useDeclareMissingReceiptMutation,
  useDeclareMissingReceiptSelfMutation
} from "../../hooks/use-expenses-queries.ts";

export interface DeclareMissingReceiptModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly expenseId: string;
  readonly isSelfService?: boolean;
}

export function DeclareMissingReceiptModal({
  isOpen,
  onClose,
  legalEntityId,
  expenseId,
  isSelfService = false
}: DeclareMissingReceiptModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const declareMutation = useDeclareMissingReceiptMutation(legalEntityId, expenseId);
  const declareSelfMutation = useDeclareMissingReceiptSelfMutation(legalEntityId, expenseId);

  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!reason.trim()) {
      setError(t("modals.declareMissingReceipt.reasonLabel"));
      return;
    }

    try {
      if (isSelfService) {
        await declareSelfMutation.mutateAsync({ reason: reason.trim() });
      } else {
        await declareMutation.mutateAsync({ reason: reason.trim() });
      }
      onClose();
      setReason("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  const isSubmitting = declareMutation.isPending || declareSelfMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.declareMissingReceipt.title")}
      description={t("modals.declareMissingReceipt.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="missing-receipt-form" isDisabled={isSubmitting}>
            {isSubmitting ? t("common.loading") : t("modals.declareMissingReceipt.submitButton")}
          </Button>
        </>
      }
    >
      <form id="missing-receipt-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              backgroundColor: "var(--color-surface-danger, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              borderRadius: "var(--radius-md, 0.5rem)",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("modals.declareMissingReceipt.reasonLabel")}
          placeholder={t("modals.declareMissingReceipt.reasonPlaceholder")}
          value={reason}
          onChange={setReason}
          isRequired
        />
      </form>
    </Dialog>
  );
}
