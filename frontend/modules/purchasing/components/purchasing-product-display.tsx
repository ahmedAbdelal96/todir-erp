import React from "react";
import { resolvePurchasingProduct } from "./purchasing-product-resolver.ts";

export interface PurchasingProductDisplayProps {
  readonly variantId?: string | null | undefined;
  readonly description?: string | undefined;
  readonly showSku?: boolean | undefined;
  readonly showUom?: boolean | undefined;
}

export function PurchasingProductDisplay({
  variantId,
  description,
  showSku = true,
  showUom = true
}: PurchasingProductDisplayProps): React.JSX.Element {
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const product = resolvePurchasingProduct(variantId, isAr);
  const displayName = description || product.name;

  return (
    <div style={{ display: "inline-flex", flexDirection: "column", gap: "0.125rem" }}>
      <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontSize: "0.875rem" }}>
        {displayName}
      </span>
      {(showSku || showUom) && (
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {showSku && (
            <span style={{ fontFamily: "monospace", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", padding: "0.125rem 0.375rem", borderRadius: "0.25rem" }}>
              {product.sku}
            </span>
          )}
          {showUom && (
            <span>· {product.uom}</span>
          )}
        </div>
      )}
    </div>
  );
}
