import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";

export interface CreateSerialModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly initialProductVariantId?: string;
  readonly onSerialCreated?: (serial: { id: string; reference: string }) => void;
}

export function CreateSerialModal({
  isOpen,
  onClose,
  legalEntityId,
  initialProductVariantId,
  onSerialCreated
}: CreateSerialModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const [productId, setProductId] = useState(initialProductVariantId ?? "a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  const [serialNumber, setSerialNumber] = useState(`SN-${Date.now().toString().slice(-5)}`);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { createSerial } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!serialNumber.trim()) {
      setErrorMessage(t("errors.serialRequired"));
      return;
    }

    try {
      const rec = await createSerial.mutateAsync({
        productVariantId: productId,
        serialNumber: serialNumber.trim()
      });

      if (onSerialCreated) {
        onSerialCreated({ id: rec.id, reference: rec.reference });
      }
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
      title={t("traceability.addSerial")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <TextField
          label={t("traceability.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <TextField
          label={t("traceability.serialNumber")}
          value={serialNumber}
          onChange={setSerialNumber}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createSerial.isPending}>
            {createSerial.isPending ? t("common.saving") : t("traceability.addSerial")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
