import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { InventoryLocation } from "../../types.ts";

export interface CreateLocationModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouseId: string;
  readonly existingLocations: readonly InventoryLocation[];
}

export function CreateLocationModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouseId,
  existingLocations
}: CreateLocationModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [locationType, setLocationType] = useState("Bin");
  const [parentLocationId, setParentLocationId] = useState<string>("");
  const [barcode, setBarcode] = useState("");
  const [canHoldStock, setCanHoldStock] = useState(true);
  const [canReceiveInto, setCanReceiveInto] = useState(true);
  const [canPickFrom, setCanPickFrom] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { createLocation } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim() || !name.trim()) {
      setErrorMessage(t("errors.generic"));
      return;
    }

    try {
      await createLocation.mutateAsync({
        warehouseId,
        parentLocationId: parentLocationId || null,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        locationType,
        barcode: barcode.trim() || null,
        canHoldStock,
        canReceiveInto,
        canPickFrom
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
      title={t("warehouses.addLocation")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <TextField
          label={t("warehouses.locationCode")}
          value={code}
          onChange={setCode}
          isRequired
        />

        <TextField
          label={t("warehouses.locationName")}
          value={name}
          onChange={setName}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("warehouses.locationType")}
            </label>
            <select
              value={locationType}
              onChange={(e) => setLocationType(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="Zone">{t("warehouses.locationTypes.Zone")}</option>
              <option value="Aisle">{t("warehouses.locationTypes.Aisle")}</option>
              <option value="Rack">{t("warehouses.locationTypes.Rack")}</option>
              <option value="Shelf">{t("warehouses.locationTypes.Shelf")}</option>
              <option value="Bin">{t("warehouses.locationTypes.Bin")}</option>
            </select>
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("warehouses.moveLocation")}
            </label>
            <select
              value={parentLocationId}
              onChange={(e) => setParentLocationId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("common.filter")}</option>
              {existingLocations.map(l => (
                <option key={l.id} value={l.id}>
                  {l.code} - {l.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <TextField
          label={t("warehouses.barcode")}
          value={barcode}
          onChange={setBarcode}
        />

        <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", marginTop: "0.25rem" }}>
          <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.8125rem" }}>
            <input
              type="checkbox"
              checked={canHoldStock}
              onChange={(e) => setCanHoldStock(e.target.checked)}
            />
            {t("warehouses.canHoldStock")}
          </label>
          <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.8125rem" }}>
            <input
              type="checkbox"
              checked={canReceiveInto}
              onChange={(e) => setCanReceiveInto(e.target.checked)}
            />
            {t("warehouses.canReceive")}
          </label>
          <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.8125rem" }}>
            <input
              type="checkbox"
              checked={canPickFrom}
              onChange={(e) => setCanPickFrom(e.target.checked)}
            />
            {t("warehouses.canPick")}
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createLocation.isPending}>
            {createLocation.isPending ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
