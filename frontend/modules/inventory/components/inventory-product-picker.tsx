import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  KNOWN_INVENTORY_PRODUCTS,
  resolveInventoryProduct,
  type ResolvedProduct
} from "./inventory-product-display.tsx";

export interface InventoryProductPickerProps {
  readonly value?: string | null;
  readonly onChange: (variantId: string) => void;
  readonly isRequired?: boolean;
  readonly label?: string;
  readonly disabled?: boolean;
}

export function InventoryProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: InventoryProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [scanMessage, setScanMessage] = useState<string | null>(null);

  const selectedProduct = value ? resolveInventoryProduct(value, isAr) : null;

  const productList = Object.values(KNOWN_INVENTORY_PRODUCTS);

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

  const handleBarcodeResolve = (code: string) => {
    setScanMessage(null);
    const trimmed = code.trim();
    if (!trimmed) return;

    const matched = productList.find(
      (p) =>
        (p.barcode && p.barcode === trimmed) ||
        p.sku.toLowerCase() === trimmed.toLowerCase() ||
        p.id === trimmed
    );

    if (matched) {
      onChange(matched.id);
      setIsSearching(false);
      setSearchQuery("");
      setScanMessage(isAr ? `تم التعرف على الصنف: ${matched.nameAr}` : `Recognized product: ${matched.nameEn}`);
      setTimeout(() => setScanMessage(null), 3000);
    } else {
      setScanMessage(isAr ? "لم يتم العثور على الصنف بالباركود المدخل" : "No product found for barcode");
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
        {label ?? t("stock.product")}
        {isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
      </label>

      {/* Selected Product Card */}
      {selectedProduct && value ? (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0.625rem 0.875rem",
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
              {selectedProduct.barcode && (
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  · {selectedProduct.barcode}
                </span>
              )}
            </div>
          </div>

          {!disabled && (
            <Button
              variant="secondary"
              onPress={() => {
                setIsSearching(!isSearching);
              }}
              type="button"
            >
              {isSearching ? t("common.close") : t("common.search")}
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
          {t("common.search")} / {t("traceability.product")}
        </Button>
      )}

      {/* Search / Scan Box */}
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
          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
            <div style={{ flex: 1 }}>
              <SearchField
                value={searchQuery}
                onChange={setSearchQuery}
                onSearch={handleBarcodeResolve}
                placeholder={t("locator.searchPlaceholder")}
                ariaLabel={t("warehouses.barcode")}
              />
            </div>
            <div style={{ alignSelf: "flex-end" }}>
              <Button
                variant="secondary"
                onPress={() => handleBarcodeResolve(searchQuery)}
                type="button"
              >
                {t("locator.locateButton")}
              </Button>
            </div>
          </div>

          {scanMessage && (
            <span style={{ fontSize: "0.75rem", color: scanMessage.includes("لم يتم") ? "var(--color-status-danger)" : "var(--color-status-success)" }}>
              {scanMessage}
            </span>
          )}

          {/* Results List */}
          <div style={{ maxHeight: "150px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
            {filteredProducts.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => {
                  onChange(p.id);
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
                <span style={{ fontSize: "0.75rem", color: "var(--color-brand-primary)" }}>
                  {p.uom}
                </span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
