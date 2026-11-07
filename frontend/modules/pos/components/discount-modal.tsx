import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";

export interface DiscountModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onApply: (discountAmount: number, reason: string) => void;
  readonly lineDescription?: string | undefined;
  readonly lineTotal: number;
  readonly currentDiscount?: number | undefined;
  readonly currencyCode?: string | undefined;
}

export function DiscountModal({
  isOpen,
  onClose,
  onApply,
  lineDescription,
  lineTotal,
  currentDiscount = 0,
  currencyCode = "EGP",
}: DiscountModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [mode, setMode] = useState<"amount" | "percent">("amount");
  const [val, setVal] = useState<string>(currentDiscount > 0 ? String(currentDiscount) : "");
  const [reason, setReason] = useState<string>("");

  const numericInput = parseFloat(val) || 0;
  let computedAmount = 0;
  if (mode === "percent") {
    const pct = Math.min(Math.max(numericInput, 0), 100);
    computedAmount = Math.round((lineTotal * pct) / 100 * 100) / 100;
  } else {
    computedAmount = Math.min(Math.max(numericInput, 0), lineTotal);
  }

  const handleApply = () => {
    onApply(computedAmount, reason);
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("discountModal.title")}
      description={lineDescription ? `${t("discountModal.item")}: ${lineDescription}` : t("discountModal.cartDiscount")}
      size="small"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={mode === "amount" ? "primary" : "secondary"}
            onClick={() => setMode("amount")}
            style={{ flex: 1, justifyContent: "center" }}
          >
            {t("discountModal.fixedAmount")}
          </Button>
          <Button
            variant={mode === "percent" ? "primary" : "secondary"}
            onClick={() => setMode("percent")}
            style={{ flex: 1, justifyContent: "center" }}
          >
            {t("discountModal.percentage")}
          </Button>
        </div>

        <TextField
          label={mode === "amount" ? `${t("discountModal.amount")} (${currencyCode})` : `${t("discountModal.percentage")}`}
          type="number"
          value={val}
          onChange={(v) => setVal(v)}
          autoFocus
        />

        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", display: "flex", justifyContent: "space-between" }}>
          <span>{t("discountModal.computedDiscount")}:</span>
          <strong style={{ color: "var(--color-content-default)" }}>
            <BidiText>{computedAmount.toFixed(2)} {currencyCode}</BidiText>
          </strong>
        </div>

        <TextField
          label={t("discountModal.reasonLabel")}
          placeholder={t("discountModal.reasonPlaceholder")}
          value={reason}
          onChange={(v) => setReason(v)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" onClick={handleApply}>
            {t("discountModal.apply")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
