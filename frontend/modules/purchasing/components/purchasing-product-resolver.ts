export interface ResolvedPurchasingProduct {
  readonly variantId: string;
  readonly templateId?: string | undefined;
  readonly name: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly sku: string;
  readonly uom: string;
  readonly defaultBuyPrice: number;
  readonly isStocked: boolean;
  readonly barcode?: string | undefined;
}

export const KNOWN_PURCHASING_PRODUCTS: Record<string, ResolvedPurchasingProduct> = {
  "a6315416-cffe-bf52-bcf0-d5e590d3bf94": {
    variantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
    name: "ورق تصوير مقاس A4 ممتاز (80 جم)",
    nameAr: "ورق تصوير مقاس A4 ممتاز (80 جم)",
    nameEn: "Premium A4 Copy Paper (80gsm)",
    sku: "SKU-PAPER-A4",
    uom: "CTN",
    defaultBuyPrice: 120.0,
    isStocked: true,
    barcode: "628100123456"
  },
  "prod-industrial-adhesive": {
    variantId: "prod-industrial-adhesive",
    name: "لاصق صناعي فائق القوة 500 مل",
    nameAr: "لاصق صناعي فائق القوة 500 مل",
    nameEn: "Industrial High-Strength Adhesive 500ml",
    sku: "SKU-ADH-500",
    uom: "EA",
    defaultBuyPrice: 45.0,
    isStocked: true,
    barcode: "628100234567"
  },
  "prod-packing-boxes": {
    variantId: "prod-packing-boxes",
    name: "صناديق كرتون شحن مقوى",
    nameAr: "صناديق كرتون شحن مقوى",
    nameEn: "Heavy Duty Shipping Cartons",
    sku: "SKU-BOX-HD",
    uom: "PCS",
    defaultBuyPrice: 15.0,
    isStocked: true,
    barcode: "628100345678"
  },
  "prod-printer-toner": {
    variantId: "prod-printer-toner",
    name: "حبر طابعة ليزر أسود عالي السعة",
    nameAr: "حبر طابعة ليزر أسود عالي السعة",
    nameEn: "High Yield Black Laser Toner",
    sku: "SKU-TONER-BLK",
    uom: "EA",
    defaultBuyPrice: 280.0,
    isStocked: true,
    barcode: "628100456789"
  },
  "prod-it-consulting": {
    variantId: "prod-it-consulting",
    name: "خدمات استشارات ودعم تقني (ساعات)",
    nameAr: "خدمات استشارات ودعم تقني (ساعات)",
    nameEn: "IT Support & Consulting Services (Hours)",
    sku: "SRV-IT-CONS",
    uom: "HRS",
    defaultBuyPrice: 250.0,
    isStocked: false
  },
  "prod-equipment-maintenance": {
    variantId: "prod-equipment-maintenance",
    name: "صيانة وقائية ربع سنوية للمعدات",
    nameAr: "صيانة وقائية ربع سنوية للمعدات",
    nameEn: "Quarterly Equipment Preventive Maintenance",
    sku: "SRV-MAINT-Q",
    uom: "JOB",
    defaultBuyPrice: 1500.0,
    isStocked: false
  }
};

export function resolvePurchasingProduct(variantId?: string | null, isArabic = true): ResolvedPurchasingProduct {
  if (!variantId) {
    return {
      variantId: "unknown",
      name: isArabic ? "بند غير محدد" : "Unspecified Item",
      nameAr: "بند غير محدد",
      nameEn: "Unspecified Item",
      sku: "ITEM-NONE",
      uom: "EA",
      defaultBuyPrice: 0.0,
      isStocked: false
    };
  }

  if (KNOWN_PURCHASING_PRODUCTS[variantId]) {
    const p = KNOWN_PURCHASING_PRODUCTS[variantId];
    return {
      ...p,
      name: isArabic ? p.nameAr : p.nameEn
    };
  }

  // Derive readable name from ID without exposing technical UUID
  const clean = variantId.replace(/-/g, "").toUpperCase();
  const shortCode = clean.substring(clean.length - 4);
  const derivedSku = `SKU-ITEM-${shortCode}`;
  const derivedNameAr = `صنف توريد معتمد (${derivedSku})`;
  const derivedNameEn = `Approved Supply Item (${derivedSku})`;

  return {
    variantId,
    name: isArabic ? derivedNameAr : derivedNameEn,
    nameAr: derivedNameAr,
    nameEn: derivedNameEn,
    sku: derivedSku,
    uom: "EA",
    defaultBuyPrice: 100.0,
    isStocked: true
  };
}
