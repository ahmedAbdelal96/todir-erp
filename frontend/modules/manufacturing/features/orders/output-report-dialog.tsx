import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useReportOutputMutation } from "../../hooks/use-manufacturing.ts";
import type { OutputType } from "../../types.ts";

export interface OutputReportDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly defaultUom?: string;
  readonly defaultVariantId?: string;
}

export function OutputReportDialog({
  isOpen,
  onClose,
  orderId,
  defaultUom = "EA",
  defaultVariantId
}: OutputReportDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outputMutation = useReportOutputMutation();

  const [quantity, setQuantity] = useState("10");
  const [outputType, setOutputType] = useState<OutputType>("Good");
  const [provisionalValue, setProvisionalValue] = useState("");
  const [currencyCode, setCurrencyCode] = useState("SAR");
  const [lotId, setLotId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [idempotencyKey] = useState(() => `out-${Date.now()}`);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const qty = parseFloat(quantity);
    if (isNaN(qty) || qty <= 0) return;

    outputMutation.mutate(
      {
        orderId,
        request: {
          quantity: qty,
          uomCode: defaultUom,
          outputType,
          idempotencyKey,
          provisionalValue: provisionalValue ? parseFloat(provisionalValue) : undefined,
          currencyCode: provisionalValue ? currencyCode : undefined,
          lotId: lotId || undefined,
          warehouseId: warehouseId || undefined,
          productVariantId: defaultVariantId
        }
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
      title={t("shopFloor.recordOutput")}
      description={t("workspace.outputsTab")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {outputMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("common.quantity")}
            type="number"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            min="0.0001"
            step="any"
            required
          />

          <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
            {t("definitions.outputType")}
            <select
              value={outputType}
              onChange={(e) => setOutputType(e.target.value as OutputType)}
              style={{
                padding: "0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-card)",
                fontSize: "0.875rem"
              }}
            >
              <option value="Good">{t("shopFloor.goodOutput")}</option>
              <option value="Primary">{t("definitions.primaryOutput")}</option>
              <option value="CoProduct">{t("definitions.coProduct")}</option>
              <option value="ByProduct">{t("definitions.byProduct")}</option>
              <option value="Waste">{t("definitions.waste")}</option>
            </select>
          </label>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("costing.unitCost")}
            type="number"
            value={provisionalValue}
            onChange={(e) => setProvisionalValue(e.target.value)}
            step="any"
          />

          <TextField
            label={t("common.currency")}
            value={currencyCode}
            onChange={(e) => setCurrencyCode(e.target.value)}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("materials.lotSerial")}
            value={lotId}
            onChange={(e) => setLotId(e.target.value)}
          />

          <TextField
            label={t("materials.warehouse")}
            value={warehouseId}
            onChange={(e) => setWarehouseId(e.target.value)}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={outputMutation.isPending || !quantity}>
            {outputMutation.isPending ? t("common.loading") : t("shopFloor.recordOutput")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
