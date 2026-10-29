import React from "react";
import { resolvePurchasingSupplier } from "./purchasing-supplier-resolver.ts";

export interface PurchasingSupplierDisplayProps {
  readonly supplierPartyId?: string | null | undefined;
  readonly showCode?: boolean | undefined;
  readonly showTaxNumber?: boolean | undefined;
  readonly showPhone?: boolean | undefined;
}

export function PurchasingSupplierDisplay({
  supplierPartyId,
  showCode = true,
  showTaxNumber = false,
  showPhone = false
}: PurchasingSupplierDisplayProps): React.JSX.Element {
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const supplier = resolvePurchasingSupplier(supplierPartyId, isAr);

  return (
    <div style={{ display: "inline-flex", flexDirection: "column", gap: "0.125rem" }}>
      <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontSize: "0.875rem" }}>
        {supplier.name}
      </span>
      {(showCode || showTaxNumber || showPhone) && (
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {showCode && (
            <span style={{ fontFamily: "monospace", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", padding: "0.125rem 0.375rem", borderRadius: "0.25rem" }}>
              {supplier.code}
            </span>
          )}
          {showTaxNumber && supplier.taxNumber && (
            <span>{supplier.taxNumber}</span>
          )}
          {showPhone && supplier.phone && (
            <span dir="ltr">{supplier.phone}</span>
          )}
        </div>
      )}
    </div>
  );
}
