import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useReverseEntry } from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { JournalEntryDto } from "../../types.ts";

export interface ReversalModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly entry: JournalEntryDto | null;
  readonly today: string;
}

export function ReversalModal({
  isOpen,
  onClose,
  legalEntityId,
  entry,
  today
}: ReversalModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("accounting");
  const reverseMutation = useReverseEntry();

  const [reversalDate, setReversalDate] = useState<string>(today);
  const [reason, setReason] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  if (!entry || !isOpen) return null;

  const handleReverse = async () => {
    setErrorMessage(null);
    if (!reversalDate || !reason.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }

    try {
      await reverseMutation.mutateAsync({
        legalEntityId,
        entryId: entry.id,
        request: {
          reversalDate,
          reason: reason.trim()
        }
      });
      onClose();
      setReason("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={`${t("reversal.title")}: ${entry.entryNumber}`}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={reverseMutation.isPending}
            onPress={handleReverse}
          >
            {t("reversal.confirmButton")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
          {t("reversal.warningText")}
        </p>

        <div
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "var(--color-surface-sunken)",
            borderRadius: "var(--radius-md, 0.5rem)",
            fontSize: "0.875rem"
          }}
        >
          <div>
            <strong>{t("journal.entryNumber")}: </strong>
            {entry.entryNumber}
          </div>
          <div>
            <strong>{t("journal.entryDate")}: </strong>
            {entry.postingDate}
          </div>
        </div>

        <TextField
          label={t("reversal.reversalDate")}
          value={reversalDate}
          onChange={setReversalDate}
          placeholder="2026-10-24"
          isRequired
        />

        <TextField
          label={t("reversal.reason")}
          value={reason}
          onChange={setReason}
          placeholder={t("reversal.reasonPlaceholder")}
          isRequired
        />

        {errorMessage && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}
      </div>
    </Dialog>
  );
}
