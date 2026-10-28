import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { Warehouse } from "../../types.ts";

export interface ReserveModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly initialProductVariantId?: string;
  readonly onReservationCreated?: (res: { id: string; requested: number; reserved: number }) => void;
}

export function ReserveModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  initialProductVariantId,
  onReservationCreated
}: ReserveModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const [productId, setProductId] = useState(initialProductVariantId ?? "a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  const [warehouseId, setWarehouseId] = useState(warehouses[0]?.id ?? "");
  const [quantity, setQuantity] = useState("2");
  const [sourceDoc, setSourceDoc] = useState("SO-2026-SEED-01");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { reserveStock } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      const idempotencyKey = `res-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      const result = await reserveStock.mutateAsync({
        productVariantId: productId,
        warehouseId,
        quantity: numQty,
        sourceModule: "inventory",
        sourceDocumentId: "00000000-0000-0000-0000-000000000001",
        idempotencyKey
      });

      if (onReservationCreated) {
        onReservationCreated({
          id: result.reservationId,
          requested: result.requestedQuantity,
          reserved: result.reservedQuantity
        });
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
      title={t("reservations.newReservation")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <InventoryProductPicker
          label={t("operations.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("operations.warehouse")}
          </label>
          <select
            value={warehouseId}
            onChange={(e) => setWarehouseId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            {warehouses.map(w => (
              <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
            ))}
          </select>
        </div>

        <TextField
          label={t("reservations.requested")}
          value={quantity}
          onChange={setQuantity}
          isRequired
        />

        <TextField
          label={t("reservations.source")}
          value={sourceDoc}
          onChange={setSourceDoc}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={reserveStock.isPending}>
            {reserveStock.isPending ? t("common.saving") : t("reservations.newReservation")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
