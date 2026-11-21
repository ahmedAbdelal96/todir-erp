import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useFirmProductionMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";
import type { PlannedSupply } from "../../types.ts";

export interface FirmProductionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly plannedSupply: PlannedSupply;
}

export function FirmProductionDialog({
  isOpen,
  onClose,
  legalEntityId,
  plannedSupply
}: FirmProductionDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const firmMutation = useFirmProductionMutation();
  const { data: plants = [] } = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState(plannedSupply.plantId ?? "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const effectivePlantId = plantId || plants[0]?.id;
    if (!effectivePlantId) return;

    firmMutation.mutate(
      {
        plannedSupplyId: plannedSupply.id,
        legalEntityId,
        plantId: effectivePlantId
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("mrp.firmProduction")}
      description={t("mrp.subtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {firmMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", background: "var(--color-surface-subtle)", padding: "0.5rem", borderRadius: "var(--radius-sm)" }}>
          <div>{t("orders.product")}: <strong>{plannedSupply.productVariantId}</strong></div>
          <div>{t("mrp.suggestedQty")}: <strong>{plannedSupply.quantity} {plannedSupply.uomCode}</strong></div>
          <div>{t("orders.needDatePrefix")}: <strong>{plannedSupply.needDate}</strong></div>
        </div>

        <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
          {t("orders.plant")}
          <select
            value={plantId || plants[0]?.id}
            onChange={(e) => setPlantId(e.target.value)}
            required
            style={{
              padding: "0.5rem",
              borderRadius: "var(--radius-sm)",
              border: "1px solid var(--color-border-default)",
              background: "var(--color-surface-card)",
              fontSize: "0.875rem"
            }}
          >
            {plants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} - {p.name}
              </option>
            ))}
          </select>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={firmMutation.isPending || plants.length === 0}>
            {firmMutation.isPending ? t("common.loading") : t("mrp.firmProduction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
