import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";

export interface ReceiveTransferModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly transferId: string;
  readonly remainingQuantity: number;
}

export function ReceiveTransferModal({
  isOpen,
  onClose,
  legalEntityId,
  transferId,
  remainingQuantity
}: ReceiveTransferModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [quantity, setQuantity] = useState(String(remainingQuantity > 0 ? remainingQuantity : 1));
  const [opDate, setOpDate] = useState(today);
  const [postDate, setPostDate] = useState(today);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { receiveTransfer } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      const idempotencyKey = `rcv-trf-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      await receiveTransfer.mutateAsync({
        transferId,
        request: {
          quantity: numQty,
          idempotencyKey,
          operationalDate: opDate,
          postingDate: postDate
        }
      });
      onClose();
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("transfers.receiveAction")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <TextField
          label={`${t("transfers.receivedQuantity")} (${t("transfers.remainingInTransit")}: ${remainingQuantity})`}
          value={quantity}
          onChange={setQuantity}
          isRequired
        />

        <LocalDateField
          label={t("operations.operationalDate")}
          value={opDate}
          onChange={(val) => setOpDate(val ?? today)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={receiveTransfer.isPending}>
            {receiveTransfer.isPending ? t("common.saving") : t("transfers.receiveAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
