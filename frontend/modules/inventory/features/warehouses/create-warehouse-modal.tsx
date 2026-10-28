import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { LocationTrackingMode } from "../../types.ts";

export interface CreateWarehouseModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreateWarehouseModal({
  isOpen,
  onClose,
  legalEntityId
}: CreateWarehouseModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [locationMode, setLocationMode] = useState<LocationTrackingMode>("WarehouseOnly");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { createWarehouse } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim() || !name.trim()) {
      setErrorMessage(t("errors.generic"));
      return;
    }

    try {
      await createWarehouse.mutateAsync({
        code: code.trim().toUpperCase(),
        name: name.trim(),
        locationMode,
        branchId: null
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
      title={t("warehouses.addWarehouse")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <TextField
          label={t("warehouses.code")}
          value={code}
          onChange={setCode}
          isRequired
        />

        <TextField
          label={t("warehouses.name")}
          value={name}
          onChange={setName}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("warehouses.mode")}
          </label>
          <select
            value={locationMode}
            onChange={(e) => setLocationMode(e.target.value as LocationTrackingMode)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            <option value="WarehouseOnly">{t("warehouses.modes.WarehouseOnly")}</option>
            <option value="Locations">{t("warehouses.modes.Locations")}</option>
            <option value="HierarchicalLocations">{t("warehouses.modes.HierarchicalLocations")}</option>
          </select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createWarehouse.isPending}>
            {createWarehouse.isPending ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
