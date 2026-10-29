import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  KNOWN_SALES_PRODUCTS,
  resolveSalesProduct,
  type ResolvedSalesProduct
} from "./sales-product-display.tsx";

export interface SalesProductPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (variantId: string, product: { name: string; sku: string; uom: string; listPrice?: number | undefined }) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export function SalesProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: SalesProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);

  const selectedProduct = value ? resolveSalesProduct(value, isAr) : null;
  const productList = Object.values(KNOWN_SALES_PRODUCTS);

  const filteredProducts = searchQuery.trim()
    ? productList.filter((p) => {
        const q = searchQuery.toLowerCase().trim();
        return (
          p.nameAr.toLowerCase().includes(q) ||
          p.nameEn.toLowerCase().includes(q) ||
          p.sku.toLowerCase().includes(q) ||
          (p.barcode && p.barcode.includes(q))
        );
      })
    : productList;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      {label && (
        <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
          {label}
          {isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
        </label>
      )}

      {/* Selected Product Card */}
      {selectedProduct && value ? (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0.5rem 0.75rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-md, 0.5rem)"
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
            <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
              {selectedProduct.name}
            </span>
            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {selectedProduct.sku}
              </span>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                · {selectedProduct.uom}
              </span>
            </div>
          </div>

          {!disabled && (
            <Button
              variant="secondary"
              onPress={() => setIsSearching(!isSearching)}
              type="button"
            >
              {isSearching ? t("common.close") : t("common.change")}
            </Button>
          )}
        </div>
      ) : (
        <Button
          variant="secondary"
          onPress={() => setIsSearching(true)}
          type="button"
          isDisabled={disabled}
        >
          {t("common.search")} / {t("quotations.lines")}
        </Button>
      )}

      {/* Search Box */}
      {(!value || isSearching) && !disabled && (
        <div
          style={{
            padding: "0.75rem",
            backgroundColor: "var(--color-surface-card)",
            border: "1px dashed var(--color-brand-primary)",
            borderRadius: "var(--radius-md)",
            display: "flex",
            flexDirection: "column",
            gap: "0.625rem"
          }}
        >
          <SearchField
            value={searchQuery}
            onChange={setSearchQuery}
            placeholder={t("quotations.searchProductPlaceholder")}
            ariaLabel={t("quotations.lines")}
          />

          {/* Results List */}
          <div style={{ maxHeight: "150px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
            {filteredProducts.map((p: ResolvedSalesProduct) => (
              <button
                key={p.id}
                type="button"
                onClick={() => {
                  onChange(p.id, {
                    name: isAr ? p.nameAr : p.nameEn,
                    sku: p.sku,
                    uom: p.uom,
                    listPrice: p.listPrice
                  });
                  setIsSearching(false);
                  setSearchQuery("");
                }}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  padding: "0.5rem 0.75rem",
                  textAlign: "start",
                  border: "1px solid var(--color-border-subtle, #e2e8f0)",
                  borderRadius: "var(--radius-sm)",
                  backgroundColor: value === p.id ? "var(--color-surface-subtle)" : "transparent",
                  cursor: "pointer",
                  fontSize: "0.8125rem"
                }}
              >
                <div>
                  <strong style={{ display: "block" }}>{isAr ? p.nameAr : p.nameEn}</strong>
                  <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                    {p.sku}
                  </span>
                </div>
                <div style={{ textAlign: "end" }}>
                  <span style={{ fontSize: "0.75rem", color: "var(--color-brand-primary)", display: "block" }}>
                    {p.uom}
                  </span>
                  {p.listPrice != null && (
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      {p.listPrice.toFixed(2)}
                    </span>
                  )}
                </div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
