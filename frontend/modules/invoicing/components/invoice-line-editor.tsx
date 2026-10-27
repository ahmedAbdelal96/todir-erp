import React, { memo } from "react";
import { useTranslation } from "react-i18next";
import { ProductPicker, type ProductPickerSelection } from "../../catalog/public.ts";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { CloseIcon, AlertCircleIcon } from "../../../components/primitives/icons.tsx";
import type { InvoiceLineInput } from "../types.ts";
import type { CurrencyCode } from "../../../components/contracts/domain-values.ts";

export interface InvoiceLineEditorProps {
  readonly line: InvoiceLineInput;
  readonly index: number;
  readonly currencyCode: CurrencyCode;
  readonly canOverridePrice: boolean;
  readonly isReadOnly?: boolean | undefined;
  readonly onChange: (index: number, updated: InvoiceLineInput) => void;
  readonly onRemove: (index: number) => void;
  readonly onShowWhyPrice?: ((index: number) => void) | undefined;
}

export const InvoiceLineEditor = memo(function InvoiceLineEditor({
  line,
  index,
  currencyCode,
  canOverridePrice,
  isReadOnly = false,
  onChange,
  onRemove,
  onShowWhyPrice
}: InvoiceLineEditorProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  const handleProductSelect = (selection: ProductPickerSelection) => {
    onChange(index, {
      ...line,
      productVariantId: selection.variantId,
      unitOfMeasureId: null,
      unitCode: selection.baseUomCode ?? null,
      description: selection.displayName
    });
  };

  const handleDescriptionChange = (val: string) => {
    onChange(index, { ...line, description: val });
  };

  const handleQuantityChange = (val: string) => {
    onChange(index, { ...line, quantity: val });
  };

  const handlePriceChange = (val: string) => {
    onChange(index, { ...line, unitPrice: val });
  };

  const handlePriceReasonChange = (val: string) => {
    onChange(index, { ...line, priceOverrideReason: val });
  };

  const handleDiscountChange = (val: string) => {
    onChange(index, { ...line, discountAmount: val });
  };

  // Safe client-side approximate calculation for immediate responsiveness
  const qtyNum = parseFloat(line.quantity) || 0;
  const priceNum = parseFloat(line.unitPrice || "0") || 0;
  const discountNum = parseFloat(line.discountAmount || "0") || 0;
  const approxNet = Math.max(0, qtyNum * priceNum - discountNum).toFixed(2);

  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "minmax(240px, 2fr) 90px 80px 130px 110px 120px 40px",
        gap: "0.5rem",
        alignItems: "center",
        padding: "0.625rem 0.75rem",
        backgroundColor: index % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)",
        borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
        fontSize: "0.8125rem"
      }}
    >
      {/* 1. Item / Description & Product Picker */}
      <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
        {!isReadOnly && !line.productVariantId ? (
          <div style={{ display: "flex", gap: "0.375rem", alignItems: "center" }}>
            <div style={{ flex: 1 }}>
              <TextField
                size="compact"
                value={line.description}
                onChange={handleDescriptionChange}
                placeholder={t("draft.itemSearch")}
                aria-label={t("draft.description")}
              />
            </div>
            <div style={{ flexShrink: 0 }}>
              <ProductPicker
                label={t("draft.selectProduct")}
                value={line.productVariantId ?? undefined}
                onChange={handleProductSelect}
              />
            </div>
          </div>
        ) : (
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "0.5rem" }}>
            <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
              {line.description || "—"}
            </span>
            {!isReadOnly && (
              <button
                type="button"
                onClick={() => onChange(index, { ...line, productVariantId: null, unitOfMeasureId: null, unitCode: null })}
                style={{
                  background: "none",
                  border: "none",
                  color: "#3b82f6",
                  fontSize: "0.75rem",
                  cursor: "pointer",
                  padding: "0.125rem 0.25rem"
                }}
              >
                {t("draft.changeProduct")}
              </button>
            )}
          </div>
        )}

        {/* Price override reason field if price has been manually overridden */}
        {line.unitPrice && canOverridePrice && !isReadOnly && (
          <div style={{ marginTop: "0.25rem" }}>
            <TextField
              size="compact"
              value={line.priceOverrideReason ?? ""}
              onChange={handlePriceReasonChange}
              placeholder={t("draft.priceOverrideReasonPlaceholder")}
              aria-label={t("draft.priceOverrideReason")}
            />
          </div>
        )}
      </div>

      {/* 2. Quantity */}
      <div>
        <TextField
          size="compact"
          value={line.quantity}
          onChange={handleQuantityChange}
          isDisabled={isReadOnly}
          aria-label={t("draft.quantity")}
        />
      </div>

      {/* 3. Unit Code */}
      <div style={{ textAlign: "center", color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
        <bdi dir="ltr">{line.unitCode || t("draft.defaultUnit")}</bdi>
      </div>

      {/* 4. Unit Price & Override */}
      <div style={{ display: "flex", alignItems: "center", gap: "0.25rem" }}>
        <TextField
          size="compact"
          value={line.unitPrice ?? ""}
          onChange={handlePriceChange}
          isDisabled={isReadOnly || !canOverridePrice}
          placeholder="0.00"
          aria-label={t("draft.unitPrice")}
        />
        {onShowWhyPrice && (
          <button
            type="button"
            onClick={() => onShowWhyPrice(index)}
            title={t("draft.whyPrice")}
            style={{
              background: "none",
              border: "none",
              color: "#3b82f6",
              cursor: "pointer",
              padding: "0.25rem",
              display: "inline-flex"
            }}
          >
            <AlertCircleIcon size={14} />
          </button>
        )}
      </div>

      {/* 5. Discount Amount */}
      <div>
        <TextField
          size="compact"
          value={line.discountAmount ?? ""}
          onChange={handleDiscountChange}
          isDisabled={isReadOnly}
          placeholder="0.00"
          aria-label={t("draft.discount")}
        />
      </div>

      {/* 6. Net Total (Approximate) */}
      <div style={{ textAlign: "end", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
        <MoneyDisplay value={{ amount: approxNet, currency: currencyCode }} />
      </div>

      {/* 7. Remove Action */}
      <div style={{ textAlign: "center" }}>
        {!isReadOnly && (
          <button
            type="button"
            onClick={() => onRemove(index)}
            aria-label={t("draft.removeLine")}
            style={{
              background: "none",
              border: "none",
              color: "#ef4444",
              cursor: "pointer",
              padding: "0.25rem",
              display: "inline-flex"
            }}
          >
            <CloseIcon size={14} />
          </button>
        )}
      </div>
    </div>
  );
});
