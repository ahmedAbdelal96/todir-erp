import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useAllocateOutputCostMutation } from "../../hooks/use-manufacturing.ts";

export interface AllocateCostDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly outputId: string;
}

export function AllocateCostDialog({ isOpen, onClose, orderId, outputId }: AllocateCostDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const allocateMutation = useAllocateOutputCostMutation();

  const [allocationBasis, setAllocationBasis] = useState<string>("RelativeSalesValue");
  const [allocationValue, setAllocationValue] = useState("1.0");
  const [allocatedAmount, setAllocatedAmount] = useState("");
  const [currencyCode, setCurrencyCode] = useState("SAR");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const amountVal = parseFloat(allocatedAmount);
    if (isNaN(amountVal) || amountVal <= 0) return;

    allocateMutation.mutate(
      {
        orderId,
        request: {
          outputId,
          allocationBasis,
          allocationValue: parseFloat(allocationValue) || 1.0,
          allocatedAmount: amountVal,
          currencyCode: currencyCode.toUpperCase(),
          idempotencyKey: `cost-alloc-${outputId}-${Date.now()}`
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const isLoading = allocateMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("costing.allocateCost")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("costing.allocationBasis")}
          </label>
          <select
            value={allocationBasis}
            onChange={(e) => setAllocationBasis(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="RelativeSalesValue">{t("costing.allocationBasisRsv")}</option>
            <option value="PhysicalWeight">{t("costing.allocationBasisWeight")}</option>
            <option value="StandardFixed">{t("costing.allocationBasisStandard")}</option>
          </select>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("costing.allocationValue")}
            type="number"
            value={allocationValue}
            onChange={(e) => setAllocationValue(e.target.value)}
            required
          />
          <TextField
            label={t("costing.allocatedAmount")}
            type="number"
            value={allocatedAmount}
            onChange={(e) => setAllocatedAmount(e.target.value)}
            required
          />
        </div>

        <TextField
          label={t("common.currency")}
          value={currencyCode}
          onChange={(e) => setCurrencyCode(e.target.value)}
          required
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !allocatedAmount}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
