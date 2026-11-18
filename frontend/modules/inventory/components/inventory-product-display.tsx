import React from "react";

export interface ResolvedProduct {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly sku: string;
  readonly uom: string;
  readonly barcode?: string | undefined;
}

export const KNOWN_INVENTORY_PRODUCTS: Record<string, ResolvedProduct> = {
  "a6315416-cffe-bf52-bcf0-d5e590d3bf94": {
    id: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
    nameAr: "سائل تنظيف أرضيات 5 لتر",
    nameEn: "Floor Cleaner Concentrated 5L",
    sku: "SKU-CLEAN-005",
    uom: "EA",
    barcode: "62210010005"
  },
  "SKU-ELEC-2002": {
    id: "SKU-ELEC-2002",
    nameAr: "كابل نحاس معزول 2.5 مم",
    nameEn: "Insulated Copper Cable 2.5mm",
    sku: "SKU-ELEC-2002",
    uom: "M",
    barcode: "62210010002"
  },
  "prod-sku-001": {
    id: "prod-sku-001",
    nameAr: "سائل تنظيف أرضيات 5 لتر",
    nameEn: "Floor Cleaner Concentrated 5L",
    sku: "SKU-CLEAN-005",
    uom: "EA",
    barcode: "62210010005"
  },
  "SKU-OFFICE-1001": {
    id: "SKU-OFFICE-1001",
    nameAr: "ورق تصوير أبيض A4 وزن 80 جم",
    nameEn: "A4 Copy Paper 80 gsm",
    sku: "SKU-OFFICE-1001",
    uom: "BOX",
    barcode: "62210010001"
  },
  "SKU-SAFE-3003": {
    id: "SKU-SAFE-3003",
    nameAr: "قفازات حماية صناعية",
    nameEn: "Industrial Safety Gloves",
    sku: "SKU-SAFE-3003",
    uom: "PACK",
    barcode: "62210010003"
  }
};

export function resolveInventoryProduct(variantId?: string | null, isAr = true): { name: string; sku: string; uom: string; barcode?: string | undefined } {
  if (!variantId) {
    return { name: "—", sku: "—", uom: "EA" };
  }

  const known = KNOWN_INVENTORY_PRODUCTS[variantId];
  if (known) {
    return {
      name: isAr ? known.nameAr : known.nameEn,
      sku: known.sku,
      uom: known.uom,
      barcode: known.barcode
    };
  }

  // Unknown identifiers must not be turned into invented names or SKU values.
  if (variantId.includes("-") && variantId.length >= 32) {
    return {
      name: isAr ? "بيانات الصنف غير متاحة" : "Product data unavailable",
      sku: "—",
      uom: "EA"
    };
  }

  return {
    name: variantId,
    sku: variantId,
    uom: "EA"
  };
}

export interface InventoryProductDisplayProps {
  readonly productVariantId?: string | null;
  readonly displayName?: string | null | undefined;
  readonly sku?: string | null | undefined;
  readonly showSkuOnly?: boolean;
  readonly inline?: boolean;
}

export function InventoryProductDisplay({
  productVariantId,
  displayName,
  sku,
  showSkuOnly = false,
  inline = false
}: InventoryProductDisplayProps): React.JSX.Element {
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const resolved = resolveInventoryProduct(productVariantId, isAr);
  const safeSku = sku?.trim() && !looksLikeInternalIdentifier(sku.trim()) ? sku.trim() : resolved.sku;
  const product = {
    ...resolved,
    name: displayName?.trim() || resolved.name,
    sku: safeSku
  };

  if (showSkuOnly) {
    return (
      <span style={{ fontFamily: "monospace", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
        {product.sku}
      </span>
    );
  }

  if (inline) {
    return (
      <span style={{ fontSize: "0.875rem" }}>
        <strong>{product.name}</strong>
        {" "}
        <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          ({product.sku})
        </span>
      </span>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
      <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontSize: "0.875rem" }}>
        {product.name}
      </span>
      <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
        {product.sku}
      </span>
    </div>
  );
}

function looksLikeInternalIdentifier(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f-]{27,}$/i.test(value) || /^[0-9a-f]{32}$/i.test(value);
}
