import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { useInventoryMutations, useLocations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { TransferMode, Warehouse } from "../../types.ts";

export interface TransferModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly initialProductVariantId?: string;
}

export function TransferModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  initialProductVariantId
}: TransferModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [productId, setProductId] = useState(initialProductVariantId ?? "a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  const [sourceWarehouseId, setSourceWarehouseId] = useState(warehouses[0]?.id ?? "");
  const [sourceLocationId, setSourceLocationId] = useState("");
  const [destWarehouseId, setDestWarehouseId] = useState(warehouses[1]?.id ?? (warehouses[0]?.id ?? ""));
  const [destLocationId, setDestLocationId] = useState("");
  const [mode, setMode] = useState<TransferMode>("Instant");
  const [quantity, setQuantity] = useState("5");
  const [uomId, setUomId] = useState("2b12a81b-ea82-a151-af7f-4c60a017f44d");
  const [opDate, setOpDate] = useState(today);
  const [postDate, setPostDate] = useState(today);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const sourceWh = warehouses.find(w => w.id === sourceWarehouseId);
  const hasSourceLocs = sourceWh && sourceWh.locationMode !== "WarehouseOnly";
  const { data: sourceLocs = [] } = useLocations(legalEntityId, hasSourceLocs ? sourceWarehouseId : undefined);

  const destWh = warehouses.find(w => w.id === destWarehouseId);
  const hasDestLocs = destWh && destWh.locationMode !== "WarehouseOnly";
  const { data: destLocs = [] } = useLocations(legalEntityId, hasDestLocs ? destWarehouseId : undefined);

  const { createTransfer } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      const idempotencyKey = `trf-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      await createTransfer.mutateAsync({
        productVariantId: productId,
        sourceWarehouseId,
        sourceLocationId: hasSourceLocs && sourceLocationId ? sourceLocationId : null,
        destinationWarehouseId: destWarehouseId,
        destinationLocationId: hasDestLocs && destLocationId ? destLocationId : null,
        enteredUomId: uomId,
        enteredQuantity: numQty,
        mode,
        idempotencyKey,
        operationalDate: opDate,
        postingDate: postDate
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
      title={t("transfers.newTransfer")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <TextField
          label={t("operations.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("transfers.mode")}
          </label>
          <select
            value={mode}
            onChange={(e) => setMode(e.target.value as TransferMode)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            <option value="Instant">{t("transfers.modes.Instant")}</option>
            <option value="InTransit">{t("transfers.modes.InTransit")}</option>
          </select>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("transfers.sourceWarehouse")}
            </label>
            <select
              value={sourceWarehouseId}
              onChange={(e) => setSourceWarehouseId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              {warehouses.map(w => (
                <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("transfers.destWarehouse")}
            </label>
            <select
              value={destWarehouseId}
              onChange={(e) => setDestWarehouseId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              {warehouses.map(w => (
                <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
              ))}
            </select>
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("operations.quantity")}
            value={quantity}
            onChange={setQuantity}
            isRequired
          />
          <LocalDateField
            label={t("operations.operationalDate")}
            value={opDate}
            onChange={(val) => setOpDate(val ?? today)}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createTransfer.isPending}>
            {createTransfer.isPending ? t("common.saving") : t("transfers.newTransfer")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
