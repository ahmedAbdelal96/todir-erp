import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";

export interface CreateLotModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly initialProductVariantId?: string;
  readonly onLotCreated?: (lot: { id: string; number: string; expiry?: string }) => void;
}

export function CreateLotModal({
  isOpen,
  onClose,
  legalEntityId,
  initialProductVariantId,
  onLotCreated
}: CreateLotModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);
  const nextYear = new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 10);

  const [productId, setProductId] = useState(initialProductVariantId ?? "");
  const [lotNumber, setLotNumber] = useState(`LOT-${Date.now().toString().slice(-4)}`);
  const [mfgDate, setMfgDate] = useState(today);
  const [expDate, setExpDate] = useState(nextYear);
  const [supplierRef, setSupplierRef] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { createLot } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!lotNumber.trim()) {
      setErrorMessage(t("errors.lotRequired"));
      return;
    }

    try {
      const rec = await createLot.mutateAsync({
        productVariantId: productId,
        lotNumber: lotNumber.trim(),
        manufacturedOn: mfgDate || null,
        expiresOn: expDate || null,
        supplierReference: supplierRef.trim() || null
      });

      if (onLotCreated) {
        onLotCreated({ id: rec.id, number: rec.reference, expiry: expDate });
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
      title={t("traceability.addLot")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <InventoryProductPicker
          label={t("traceability.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <TextField
          label={t("traceability.lotNumber")}
          value={lotNumber}
          onChange={setLotNumber}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("traceability.manufacturedDate")}
            value={mfgDate}
            onChange={(val) => setMfgDate(val ?? today)}
          />
          <LocalDateField
            label={t("traceability.expiryDate")}
            value={expDate}
            onChange={(val) => setExpDate(val ?? nextYear)}
          />
        </div>

        <TextField
          label={t("traceability.supplierRef")}
          value={supplierRef}
          onChange={setSupplierRef}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createLot.isPending}>
            {createLot.isPending ? t("common.saving") : t("traceability.addLot")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
