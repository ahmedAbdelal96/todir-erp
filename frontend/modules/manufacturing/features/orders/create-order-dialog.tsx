import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateOrderMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";
import { manufacturingApi } from "../../services/manufacturing-api.ts";
import type { ProductionDefinition } from "../../types.ts";

export interface CreateOrderDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreateOrderDialog({ isOpen, onClose, legalEntityId }: CreateOrderDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createMutation = useCreateOrderMutation();
  const { data: plants = [] } = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState("");
  const [productVariantId, setProductVariantId] = useState("");
  const [orderedQty, setOrderedQty] = useState("");
  const [uomCode, setUomCode] = useState("EA");
  const [needDate, setNeedDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [sourceType, setSourceType] = useState("Manual");
  const [sourceRef, setSourceRef] = useState("");

  const [resolving, setResolving] = useState(false);
  const [resolvedDef, setResolvedDef] = useState<ProductionDefinition | null>(null);
  const [resolveError, setResolveError] = useState<string | null>(null);

  const handleResolveDefinition = async () => {
    if (!plantId || !productVariantId) return;
    setResolving(true);
    setResolveError(null);
    try {
      const def = await manufacturingApi.resolveDefinition({
        legalEntityId,
        plantId,
        productVariantId,
        quantity: parseFloat(orderedQty) || 1,
        asOfDate: needDate
      });
      setResolvedDef(def);
    } catch {
      setResolveError(t("errors.definitionMissing"));
      setResolvedDef(null);
    } finally {
      setResolving(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!plantId || !productVariantId || !orderedQty || !needDate) return;

    createMutation.mutate(
      {
        legalEntityId,
        plantId,
        productVariantId,
        orderedQty: parseFloat(orderedQty),
        uomCode: uomCode.trim().toUpperCase(),
        needDate,
        sourceType,
        sourceRef: sourceRef || undefined,
        productionDefinitionId: resolvedDef?.id
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
      title={t("orders.createOrder")}
      description={t("orders.subtitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {createMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
            {t("orders.plant")}
            <select
              value={plantId}
              onChange={(e) => {
                setPlantId(e.target.value);
                setResolvedDef(null);
              }}
              required
              style={{
                padding: "0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-card)",
                fontSize: "0.875rem"
              }}
            >
              <option value="">{t("common.filterByPlant")}</option>
              {plants.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} - {p.name}
                </option>
              ))}
            </select>
          </label>

          <TextField
            label={t("orders.product")}
            value={productVariantId}
            onChange={(e) => {
              setProductVariantId(e.target.value);
              setResolvedDef(null);
            }}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("orders.quantity")}
            type="number"
            value={orderedQty}
            onChange={(e) => setOrderedQty(e.target.value)}
            min="0.0001"
            step="any"
            required
          />

          <TextField
            label={t("orders.uom")}
            value={uomCode}
            onChange={(e) => setUomCode(e.target.value)}
            required
          />

          <TextField
            label={t("orders.needDate")}
            type="date"
            value={needDate}
            onChange={(e) => setNeedDate(e.target.value)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
            {t("orders.sourceType")}
            <select
              value={sourceType}
              onChange={(e) => setSourceType(e.target.value)}
              style={{
                padding: "0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-card)",
                fontSize: "0.875rem"
              }}
            >
              <option value="Manual">Manual</option>
              <option value="Sales">Sales</option>
              <option value="MRP">MRP</option>
              <option value="Project">Project</option>
            </select>
          </label>

          <TextField
            label={t("orders.sourceRef")}
            value={sourceRef}
            onChange={(e) => setSourceRef(e.target.value)}
          />
        </div>

        {/* Definition Resolution Preview */}
        <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle, #f8fafc)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <div>
            <div style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
              {t("workspace.definitionSnapshot")}
            </div>
            <div style={{ fontSize: "0.75rem", color: resolvedDef ? "#047857" : "var(--color-content-muted)" }}>
              {resolvedDef ? resolvedDef.manufacturingMode : (resolveError ?? t("definitions.definitionsTab"))}
            </div>
          </div>
          <Button
            type="button"
            variant="outline"
            size="small"
            onPress={handleResolveDefinition}
            isDisabled={resolving || !plantId || !productVariantId}
          >
            {resolving ? t("common.loading") : t("common.confirm")}
          </Button>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending || !plantId || !productVariantId}>
            {createMutation.isPending ? t("common.loading") : t("orders.createOrder")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
