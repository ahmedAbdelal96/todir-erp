import React from "react";
import {
  type ResolvedSalesProduct,
  KNOWN_SALES_PRODUCTS,
  resolveSalesProduct
} from "./sales-product-resolver.js";

export type { ResolvedSalesProduct };
export { KNOWN_SALES_PRODUCTS, resolveSalesProduct };

export interface SalesProductDisplayProps {
  readonly productVariantId?: string | null | undefined;
  readonly showSkuOnly?: boolean | undefined;
  readonly inline?: boolean | undefined;
}

export function SalesProductDisplay({
  productVariantId,
  showSkuOnly = false,
  inline = false
}: SalesProductDisplayProps): React.JSX.Element {
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const resolved = resolveSalesProduct(productVariantId, isAr);

  if (showSkuOnly) {
    return (
      <span style={{ fontFamily: "monospace", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
        {resolved.sku}
      </span>
    );
  }

  if (inline) {
    return (
      <span style={{ fontSize: "0.875rem" }}>
        <strong>{resolved.name}</strong>
        {" "}
        <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          ({resolved.sku})
        </span>
      </span>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
      <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontSize: "0.875rem" }}>
        {resolved.name}
      </span>
      <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
        {resolved.sku}
      </span>
    </div>
  );
}
