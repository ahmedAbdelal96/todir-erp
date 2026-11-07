import React from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { HoldIcon, CashIcon } from "../../components/pos-icons.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";

export interface PosCartTotalsPanelProps {
  readonly subtotal: number;
  readonly taxTotal: number;
  readonly discountTotal: number;
  readonly grandTotal: number;
  readonly currencyCode?: string | undefined;
  readonly onOpenTender: () => void;
  readonly onHoldCart: () => void;
  readonly onCancelCart: () => void;
  readonly isHeld?: boolean | undefined;
  readonly isSubmitting?: boolean | undefined;
  readonly lineCount: number;
}

export function PosCartTotalsPanel({
  subtotal,
  taxTotal,
  discountTotal,
  grandTotal,
  currencyCode = "EGP",
  onOpenTender,
  onHoldCart,
  onCancelCart,
  isHeld = false,
  isSubmitting = false,
  lineCount,
}: PosCartTotalsPanelProps): React.JSX.Element {
  const { t } = useTranslation("pos");

  return (
    <div
      style={{
        backgroundColor: "#ffffff",
        borderRadius: "var(--radius-lg)",
        border: "1px solid #cbd5e1",
        boxShadow: "0 2px 4px 0 rgba(0, 0, 0, 0.05)",
        padding: "1.25rem",
        display: "flex",
        flexDirection: "column",
        gap: "1rem",
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {/* Subtotal */}
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          <span>{t("totals.subtotal")}:</span>
          <span style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
            <BidiText>{subtotal.toFixed(2)} {currencyCode}</BidiText>
          </span>
        </div>

        {/* Discount */}
        {discountTotal > 0 && (
          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem", color: "var(--color-status-success)" }}>
            <span>{t("totals.discount")}:</span>
            <span style={{ fontWeight: 600 }}>
              -<BidiText>{discountTotal.toFixed(2)} {currencyCode}</BidiText>
            </span>
          </div>
        )}

        {/* Tax */}
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          <span>{t("totals.tax")}:</span>
          <span style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
            <BidiText>{taxTotal.toFixed(2)} {currencyCode}</BidiText>
          </span>
        </div>

        {/* Divider */}
        <div style={{ borderTop: "2px solid #cbd5e1", margin: "0.25rem 0" }} />

        {/* Grand Total */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
          <span style={{ fontSize: "1.125rem", fontWeight: 700, color: "#102a43" }}>
            {t("totals.grandTotal")}:
          </span>
          <span style={{ fontSize: "2rem", fontWeight: 900, color: "var(--color-brand-primary)", lineHeight: 1.1 }}>
            <BidiText>{grandTotal.toFixed(2)} {currencyCode}</BidiText>
          </span>
        </div>
      </div>

      {/* Primary Pay Action */}
      <Button
        variant="primary"
        size="comfortable"
        isDisabled={isSubmitting || lineCount === 0 || isHeld}
        onClick={onOpenTender}
        style={{
          width: "100%",
          justifyContent: "center",
          fontSize: "1.125rem",
          fontWeight: 700,
          padding: "0.875rem 1.5rem",
          display: "flex",
          gap: "0.5rem",
          alignItems: "center",
        }}
      >
        <CashIcon size={22} />
        <span>{t("totals.payNow")}</span>
      </Button>

      {/* Secondary Actions (Hold / Cancel) */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant="secondary"
          isDisabled={isSubmitting || lineCount === 0}
          onClick={onHoldCart}
          style={{ flex: 1, justifyContent: "center", display: "flex", gap: "0.35rem", alignItems: "center" }}
        >
          <HoldIcon size={16} />
          <span>{isHeld ? t("totals.resumeCart") : t("totals.holdCart")}</span>
        </Button>

        <Button
          variant="secondary"
          isDisabled={isSubmitting || lineCount === 0}
          onClick={onCancelCart}
          style={{ flex: 1, justifyContent: "center", color: "var(--color-status-danger)" }}
        >
          {t("totals.cancelCart")}
        </Button>
      </div>
    </div>
  );
}
