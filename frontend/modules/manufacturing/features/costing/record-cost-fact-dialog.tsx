import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useRecordCostFactMutation } from "../../hooks/use-manufacturing.ts";

export interface RecordCostFactDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
}

export function RecordCostFactDialog({ isOpen, onClose, orderId }: RecordCostFactDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const recordCostMutation = useRecordCostFactMutation();

  const [costCategory, setCostCategory] = useState<"Material" | "Labor" | "Machine" | "Overhead" | "Subcontract">("Overhead");
  const [amount, setAmount] = useState("");
  const [currencyCode, setCurrencyCode] = useState("SAR");
  const [sourceType, setSourceType] = useState("ManualVoucher");
  const [sourceId, setSourceId] = useState("");
  const [sourceDate, setSourceDate] = useState(new Date().toISOString().slice(0, 10));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const costVal = parseFloat(amount);
    if (isNaN(costVal) || costVal <= 0) return;

    recordCostMutation.mutate(
      {
        orderId,
        request: {
          costCategory,
          amount: costVal,
          currencyCode: currencyCode.toUpperCase(),
          sourceType: sourceType.trim() || "ManualVoucher",
          sourceId: sourceId.trim() || `CST-${Date.now()}`,
          sourceDate,
          idempotencyKey: `cst-fact-${orderId}-${Date.now()}`
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const isLoading = recordCostMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("costing.recordCostFact")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("costing.costCategory")}
          </label>
          <select
            value={costCategory}
            onChange={(e) => setCostCategory(e.target.value as any)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="Overhead">{t("costing.costOverhead")}</option>
            <option value="Labor">{t("costing.costLabor")}</option>
            <option value="Machine">{t("costing.costMachine")}</option>
            <option value="Material">{t("costing.costMaterial")}</option>
            <option value="Subcontract">{t("costing.costSubcontract")}</option>
          </select>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("costing.costAmount")}
            type="number"
            value={amount}
            onChange={(val) => setAmount(val)}
            required
          />
          <TextField
            label={t("common.currency")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("common.sourceType")}
            value={sourceType}
            onChange={(val) => setSourceType(val)}
          />
          <TextField
            label={t("common.sourceId")}
            value={sourceId}
            onChange={(val) => setSourceId(val)}
          />
        </div>

        <TextField
          label={t("common.sourceDate")}
          type="date"
          value={sourceDate}
          onChange={(val) => setSourceDate(val)}
          required
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !amount}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
