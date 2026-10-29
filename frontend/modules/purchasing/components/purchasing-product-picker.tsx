import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  KNOWN_PURCHASING_PRODUCTS,
  resolvePurchasingProduct,
  type ResolvedPurchasingProduct
} from "./purchasing-product-resolver.ts";

export interface PurchasingProductPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (variantId: string, product: { name: string; sku: string; uom: string; defaultBuyPrice: number; isStocked: boolean }) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export function PurchasingProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: PurchasingProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);

  const selectedProduct = value ? resolvePurchasingProduct(value, isAr) : null;
  const productList = Object.values(KNOWN_PURCHASING_PRODUCTS);

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

  const handleSelect = (product: ResolvedPurchasingProduct) => {
    onChange(product.variantId, {
      name: isAr ? product.nameAr : product.nameEn,
      sku: product.sku,
      uom: product.uom,
      defaultBuyPrice: product.defaultBuyPrice,
      isStocked: product.isStocked
    });
    setIsSearching(false);
    setSearchQuery("");
  };

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
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                · {selectedProduct.isStocked ? t("products.stocked") : t("products.service")}
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
          {t("common.search")} / {t("orders.lines")}
        </Button>
      )}

      {/* Product Selection Modal */}
      {isSearching && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: "rgba(15, 23, 42, 0.6)",
            backdropFilter: "blur(2px)",
            display: "grid",
            placeItems: "center",
            zIndex: 1000,
            padding: "1rem"
          }}
        >
          <div
            style={{
              backgroundColor: "var(--color-surface-card, #ffffff)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              borderRadius: "var(--radius-lg, 0.75rem)",
              boxShadow: "var(--shadow-elevation-high)",
              width: "100%",
              maxWidth: "38rem",
              maxHeight: "85vh",
              display: "flex",
              flexDirection: "column",
              overflow: "hidden"
            }}
          >
            {/* Header */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "1rem 1.25rem",
                borderBottom: "1px solid var(--color-border-default)"
              }}
            >
              <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("products.selectTitle")}
              </h2>
              <Button
                variant="secondary"
                onPress={() => setIsSearching(false)}
                type="button"
              >
                {t("common.close")}
              </Button>
            </div>

            {/* Search Input */}
            <div style={{ padding: "1rem 1.25rem 0.5rem" }}>
              <SearchField
                value={searchQuery}
                onChange={setSearchQuery}
                placeholder={t("products.searchPlaceholder")}
                aria-label={t("products.searchPlaceholder")}
              />
            </div>

            {/* Product List */}
            <div style={{ flex: 1, overflowY: "auto", padding: "0.5rem 1.25rem 1.25rem" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                {filteredProducts.map((p) => (
                  <div
                    key={p.variantId}
                    onClick={() => handleSelect(p)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleSelect(p);
                      }
                    }}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "0.75rem 1rem",
                      borderRadius: "var(--radius-md, 0.5rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: value === p.variantId
                        ? "var(--color-brand-primary-light, #eff6ff)"
                        : "var(--color-surface-card, #ffffff)",
                      cursor: "pointer",
                      transition: "background-color 0.15s ease"
                    }}
                  >
                    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>
                        {isAr ? p.nameAr : p.nameEn}
                      </span>
                      <div style={{ display: "flex", gap: "0.625rem", alignItems: "center", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        <span style={{ fontFamily: "monospace" }}>{p.sku}</span>
                        <span>· {p.uom}</span>
                        <span>· {p.isStocked ? t("products.stocked") : t("products.service")}</span>
                        {p.barcode && <span>· {p.barcode}</span>}
                      </div>
                    </div>

                    <Button
                      variant={value === p.variantId ? "primary" : "secondary"}
                      onPress={() => handleSelect(p)}
                      type="button"
                    >
                      {value === p.variantId ? t("common.selected") : t("common.select")}
                    </Button>
                  </div>
                ))}

                {filteredProducts.length === 0 && (
                  <div style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
                    <p style={{ margin: 0 }}>{t("products.noResults")}</p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
