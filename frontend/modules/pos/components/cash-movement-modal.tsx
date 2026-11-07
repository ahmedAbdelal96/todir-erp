import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import type { CashMovementType } from "../types.ts";

export interface CashMovementModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onConfirm: (type: CashMovementType, amount: number, reason: string) => Promise<void>;
  readonly currencyCode?: string | undefined;
  readonly isSubmitting?: boolean | undefined;
}

export function CashMovementModal({
  isOpen,
  onClose,
  onConfirm,
  currencyCode = "EGP",
  isSubmitting = false,
}: CashMovementModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [movementType, setMovementType] = useState<CashMovementType>("CashIn");
  const [amountStr, setAmountStr] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const amount = parseFloat(amountStr) || 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (amount <= 0) {
      setError(t("cashMovement.invalidAmount"));
      return;
    }
    if (!reason.trim()) {
      setError(t("cashMovement.reasonRequired"));
      return;
    }
    setError(null);
    try {
      await onConfirm(movementType, amount, reason.trim());
      setAmountStr("");
      setReason("");
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("cashMovement.failed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("cashMovement.title")}
      description={t("cashMovement.subtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("cashMovement.typeLabel")}
          selectedKey={movementType}
          onSelectionChange={(val) => setMovementType(String(val) as CashMovementType)}
          options={[
            { id: "CashIn", label: t("cashMovement.cashIn") },
            { id: "CashOut", label: t("cashMovement.cashOut") },
            { id: "Drop", label: t("cashMovement.cashDrop") },
          ]}
        />

        <TextField
          label={`${t("cashMovement.amountLabel")} (${currencyCode})`}
          type="number"
          value={amountStr}
          onChange={(v) => {
            setAmountStr(v);
            setError(null);
          }}
          autoFocus
        />

        <TextField
          label={t("cashMovement.reasonLabel")}
          placeholder={t("cashMovement.reasonPlaceholder")}
          value={reason}
          onChange={(v) => {
            setReason(v);
            setError(null);
          }}
        />

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting || amount <= 0}>
            {isSubmitting ? t("common.submitting") : t("cashMovement.confirmAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
