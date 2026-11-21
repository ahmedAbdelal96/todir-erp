import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreatePlanningRunMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";

export interface CreatePlanningRunDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreatePlanningRunDialog({
  isOpen,
  onClose,
  legalEntityId
}: CreatePlanningRunDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createMutation = useCreatePlanningRunMutation();
  const { data: plants = [] } = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState("");
  const [horizonStart, setHorizonStart] = useState(() => new Date().toISOString().slice(0, 10));
  const [horizonEnd, setHorizonEnd] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return d.toISOString().slice(0, 10);
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!horizonStart || !horizonEnd) return;

    createMutation.mutate(
      {
        legalEntityId,
        plantId: plantId || undefined,
        horizonStart,
        horizonEnd
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
      title={t("mrp.launchRun")}
      description={t("mrp.subtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {createMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
          {t("orders.plant")}
          <select
            value={plantId}
            onChange={(e) => setPlantId(e.target.value)}
            style={{
              padding: "0.5rem",
              borderRadius: "var(--radius-sm)",
              border: "1px solid var(--color-border-default)",
              background: "var(--color-surface-card)",
              fontSize: "0.875rem"
            }}
          >
            <option value="">{t("common.allPlants")}</option>
            {plants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} - {p.name}
              </option>
            ))}
          </select>
        </label>

        <TextField
          label={t("mrp.horizonStart")}
          type="date"
          value={horizonStart}
          onChange={(e) => setHorizonStart(e.target.value)}
          required
        />

        <TextField
          label={t("mrp.horizonEnd")}
          type="date"
          value={horizonEnd}
          onChange={(e) => setHorizonEnd(e.target.value)}
          required
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending || !horizonStart || !horizonEnd}>
            {createMutation.isPending ? t("common.loading") : t("mrp.launchRun")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
