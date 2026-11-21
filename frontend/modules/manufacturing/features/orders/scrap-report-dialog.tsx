import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useReportScrapMutation } from "../../hooks/use-manufacturing.ts";

export interface ScrapReportDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly defaultUom?: string;
  readonly operationId?: string;
}

export function ScrapReportDialog({
  isOpen,
  onClose,
  orderId,
  defaultUom = "EA",
  operationId
}: ScrapReportDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const scrapMutation = useReportScrapMutation();

  const [quantity, setQuantity] = useState("1");
  const [category, setCategory] = useState("Defect");
  const [reason, setReason] = useState("");
  const [idempotencyKey] = useState(() => `scrap-${Date.now()}`);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const qty = parseFloat(quantity);
    if (isNaN(qty) || qty <= 0) return;

    scrapMutation.mutate(
      {
        orderId,
        request: {
          quantity: qty,
          uomCode: defaultUom,
          category,
          reason: reason || undefined,
          idempotencyKey,
          operationId: operationId || undefined
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
      title={t("shopFloor.reportScrap")}
      description={t("orders.totalScrapOutput")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {scrapMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <TextField
          label={t("orders.scrapQty")}
          type="number"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          min="0.0001"
          step="any"
          required
        />

        <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
          {t("costing.costCategory")}
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            style={{
              padding: "0.5rem",
              borderRadius: "var(--radius-sm)",
              border: "1px solid var(--color-border-default)",
              background: "var(--color-surface-card)",
              fontSize: "0.875rem"
            }}
          >
            <option value="Defect">Defect</option>
            <option value="MachineError">Machine Error</option>
            <option value="SetupWaste">Setup Waste</option>
            <option value="RawMaterialFault">Raw Material Fault</option>
            <option value="OperatorMistake">Operator Mistake</option>
          </select>
        </label>

        <TextField
          label={t("orders.reasonLabel")}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="destructive" isDisabled={scrapMutation.isPending || !quantity}>
            {scrapMutation.isPending ? t("common.loading") : t("shopFloor.reportScrap")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
