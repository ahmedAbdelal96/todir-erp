import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { PhysicalCountMode, Warehouse } from "../../types.ts";

export interface CreateCountModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly onCountCreated?: (count: { id: string; number: string; warehouseId: string; mode: string }) => void;
}

export function CreateCountModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  onCountCreated
}: CreateCountModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const [warehouseId, setWarehouseId] = useState(warehouses[0]?.id ?? "");
  const [mode, setMode] = useState<PhysicalCountMode>("Simple");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { createPhysicalCount } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    try {
      const result = await createPhysicalCount.mutateAsync({
        warehouseId,
        mode
      });

      if (onCountCreated) {
        onCountCreated({
          id: result.id,
          number: result.number,
          warehouseId,
          mode
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
      title={t("counts.newCount")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("counts.warehouse")}
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

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("counts.mode")}
          </label>
          <select
            value={mode}
            onChange={(e) => setMode(e.target.value as PhysicalCountMode)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            <option value="Simple">{t("counts.modes.Simple")}</option>
            <option value="Full">{t("counts.modes.Full")}</option>
            <option value="Cycle">{t("counts.modes.Cycle")}</option>
            <option value="Blind">{t("counts.modes.Blind")}</option>
          </select>
        </div>

        {mode === "Blind" && (
          <div style={{ padding: "0.75rem", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#92400e" }}>
            {t("counts.blindCountNote")}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createPhysicalCount.isPending}>
            {createPhysicalCount.isPending ? t("common.saving") : t("counts.newCount")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
