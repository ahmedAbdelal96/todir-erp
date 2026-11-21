import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateDefinitionMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";
import type { ManufacturingMode } from "../../types.ts";

export interface CreateDefinitionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreateDefinitionDialog({ isOpen, onClose, legalEntityId }: CreateDefinitionDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createDefMutation = useCreateDefinitionMutation();
  const plantsQuery = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState("");
  const [productVariantId, setProductVariantId] = useState("");
  const [mode, setMode] = useState<ManufacturingMode>("Discrete");
  const [bomVersionId, setBomVersionId] = useState("");
  const [formulaVersionId, setFormulaVersionId] = useState("");
  const [routingVersionId, setRoutingVersionId] = useState("");
  const [validFrom, setValidFrom] = useState(new Date().toISOString().slice(0, 10));
  const [validTo, setValidTo] = useState("");
  const [priority, setPriority] = useState("10");
  const [expectedYield, setExpectedYield] = useState("1.0");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const effectivePlantId = plantId || plantsQuery.data?.[0]?.id;
    if (!effectivePlantId || !productVariantId.trim()) return;

    createDefMutation.mutate(
      {
        legalEntityId,
        plantId: effectivePlantId,
        productVariantId: productVariantId.trim(),
        manufacturingMode: mode,
        bomVersionId: mode === "Discrete" && bomVersionId.trim() ? bomVersionId.trim() : undefined,
        formulaVersionId: mode === "ProcessBatch" && formulaVersionId.trim() ? formulaVersionId.trim() : undefined,
        routingVersionId: routingVersionId.trim() || undefined,
        validFrom,
        validTo: validTo.trim() || undefined,
        priority: parseInt(priority, 10) || 10,
        expectedYield: parseFloat(expectedYield) || 1.0
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const isLoading = createDefMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addDefinition")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
              {t("orders.plant")}
            </label>
            <select
              value={plantId || plantsQuery.data?.[0]?.id || ""}
              onChange={(e) => setPlantId(e.target.value)}
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #cbd5e1",
                fontSize: "14px",
                background: "#ffffff"
              }}
              required
            >
              {plantsQuery.data?.map((plant) => (
                <option key={plant.id} value={plant.id}>
                  {plant.code} - {plant.name}
                </option>
              ))}
            </select>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
              {t("definitions.mode")}
            </label>
            <select
              value={mode}
              onChange={(e) => setMode(e.target.value as ManufacturingMode)}
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #cbd5e1",
                fontSize: "14px",
                background: "#ffffff"
              }}
            >
              <option value="Discrete">{t("modes.Discrete")}</option>
              <option value="ProcessBatch">{t("modes.ProcessBatch")}</option>
            </select>
          </div>
        </div>

        <TextField
          label={t("orders.product")}
          value={productVariantId}
          onChange={(e) => setProductVariantId(e.target.value)}
          required
        />

        {mode === "Discrete" ? (
          <TextField
            label={t("definitions.bomVersionId")}
            value={bomVersionId}
            onChange={(e) => setBomVersionId(e.target.value)}
          />
        ) : (
          <TextField
            label={t("definitions.formulaVersionId")}
            value={formulaVersionId}
            onChange={(e) => setFormulaVersionId(e.target.value)}
          />
        )}

        <TextField
          label={t("definitions.routingVersionId")}
          value={routingVersionId}
          onChange={(e) => setRoutingVersionId(e.target.value)}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("common.validFrom")}
            type="date"
            value={validFrom}
            onChange={(e) => setValidFrom(e.target.value)}
            required
          />
          <TextField
            label={t("common.validTo")}
            type="date"
            value={validTo}
            onChange={(e) => setValidTo(e.target.value)}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("common.priority")}
            type="number"
            value={priority}
            onChange={(e) => setPriority(e.target.value)}
          />
          <TextField
            label={t("definitions.expectedYield")}
            type="number"
            value={expectedYield}
            onChange={(e) => setExpectedYield(e.target.value)}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !productVariantId}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
