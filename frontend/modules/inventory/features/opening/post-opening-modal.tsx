import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { Warehouse } from "../../types.ts";

export interface PostOpeningModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canReadCost?: boolean;
  readonly onOpeningPosted?: (op: { id: string; productVariantId: string; warehouseName: string; quantity: number; legacyRef: string }) => void;
}

export function PostOpeningModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  canReadCost = false,
  onOpeningPosted
}: PostOpeningModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [productId, setProductId] = useState("a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  const [warehouseId, setWarehouseId] = useState(warehouses[0]?.id ?? "");
  const [quantity, setQuantity] = useState("100");
  const [uomId, setUomId] = useState("2b12a81b-ea82-a151-af7f-4c60a017f44d");
  const [unitCost, setUnitCost] = useState("");
  const [currency, setCurrency] = useState("SAR");
  const [legacyRef, setLegacyRef] = useState("LEGACY-ERP-INV-001");
  const [opDate, setOpDate] = useState(today);
  const [postDate, setPostDate] = useState(today);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const selectedWarehouse = warehouses.find(w => w.id === warehouseId);
  const { postOpeningStock } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      const idempotencyKey = `open-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      const result = await postOpeningStock.mutateAsync({
        productVariantId: productId,
        warehouseId,
        enteredUomId: uomId,
        enteredQuantity: numQty,
        operationalDate: opDate,
        postingDate: postDate,
        idempotencyKey,
        sourceModule: "inventory",
        unitCost: canReadCost && unitCost ? parseFloat(unitCost) : null,
        currencyCode: canReadCost && unitCost ? currency : null
      });

      if (onOpeningPosted) {
        onOpeningPosted({
          id: result.operationNumber,
          productVariantId: productId,
          warehouseName: selectedWarehouse?.name ?? "المخزن الرئيسي",
          quantity: numQty,
          legacyRef
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
      title={t("opening.postOpening")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <InventoryProductPicker
          label={t("opening.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("opening.warehouse")}
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

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("opening.quantity")}
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

        {canReadCost && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <TextField
              label={t("opening.unitCost")}
              value={unitCost}
              onChange={setUnitCost}
            />
            <TextField
              label={t("operations.currency")}
              value={currency}
              onChange={setCurrency}
            />
          </div>
        )}

        <TextField
          label={t("opening.legacyReference")}
          value={legacyRef}
          onChange={setLegacyRef}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={postOpeningStock.isPending}>
            {postOpeningStock.isPending ? t("common.saving") : t("opening.postOpening")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
