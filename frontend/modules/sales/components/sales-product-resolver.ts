export interface ResolvedSalesProduct {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly sku: string;
  readonly uom: string;
  readonly barcode?: string | undefined;
  readonly listPrice?: number | undefined;
}

export const KNOWN_SALES_PRODUCTS: Record<string, ResolvedSalesProduct> = {
  "a6315416-cffe-bf52-bcf0-d5e590d3bf94": {
    id: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
    nameAr: "سائل تنظيف أرضيات 5 لتر",
    nameEn: "Floor Cleaner Concentrated 5L",
    sku: "SKU-CLEAN-005",
    uom: "EA",
    barcode: "62210010005",
    listPrice: 120
  },
  "SKU-OFFICE-1001": {
    id: "SKU-OFFICE-1001",
    nameAr: "ورق تصوير أبيض A4 وزن 80 جم",
    nameEn: "A4 Copy Paper 80 gsm",
    sku: "SKU-OFFICE-1001",
    uom: "BOX",
    barcode: "62210010001",
    listPrice: 85
  },
  "SKU-ELEC-2002": {
    id: "SKU-ELEC-2002",
    nameAr: "كابل نحاس معزول 2.5 مم",
    nameEn: "Insulated Copper Cable 2.5mm",
    sku: "SKU-ELEC-2002",
    uom: "M",
    barcode: "62210010002",
    listPrice: 45
  },
  "SKU-SAFE-3003": {
    id: "SKU-SAFE-3003",
    nameAr: "قفازات حماية صناعية",
    nameEn: "Industrial Safety Gloves",
    sku: "SKU-SAFE-3003",
    uom: "PACK",
    barcode: "62210010003",
    listPrice: 60
  },
  "prod-sku-001": {
    id: "prod-sku-001",
    nameAr: "سائل تنظيف أرضيات 5 لتر",
    nameEn: "Floor Cleaner Concentrated 5L",
    sku: "SKU-CLEAN-005",
    uom: "EA",
    barcode: "62210010005",
    listPrice: 120
  }
};

const SEED_PRODUCT_NAMES_AR = [
  "ورق تصوير أبيض A4 وزن 80 جم",
  "كابل نحاس معزول 2.5 مم",
  "شريط لاصق للتغليف 48 مم",
  "جهاز قياس رقمي متعدد الاستخدام",
  "فلتر هواء صناعي مقاس متوسط",
  "دباسة مكتبية ثقيلة",
  "مصباح LED موفر 18 وات",
  "كرتونة شحن مزدوجة الطبقة",
  "قفازات حماية صناعية",
  "حبر طابعة ليزر أسود",
  "مفتاح ربط صناعي 12 مم",
  "منظف أرضيات مركز 5 لتر",
  "كرسي مكتب شبكي",
  "قرص تخزين خارجي 1 تيرابايت",
  "شريط تحذير أرضي أصفر وأسود",
  "علبة ملفات كرتون مقوى",
  "مقياس ضغط صناعي",
  "وصلة كهرباء متعددة المخارج",
  "لفة استرتش للتغليف"
];

const SEED_PRODUCT_NAMES_EN = [
  "A4 Copy Paper 80 gsm",
  "Insulated Copper Cable 2.5 mm",
  "Packaging Tape 48 mm",
  "Digital Multimeter",
  "Industrial Air Filter Medium",
  "Heavy-Duty Office Stapler",
  "LED Lamp 18 W",
  "Double-Wall Shipping Carton",
  "Industrial Safety Gloves",
  "Black Laser Printer Toner",
  "Industrial Wrench 12 mm",
  "Concentrated Floor Cleaner 5 L",
  "Mesh Office Chair",
  "External Storage Drive 1 TB",
  "Yellow and Black Floor Marking Tape",
  "Reinforced Cardboard File Box",
  "Industrial Pressure Gauge",
  "Multi-Outlet Power Strip",
  "Pallet Stretch Wrap"
];

export function resolveSalesProduct(variantId?: string | null, isAr = true): { name: string; sku: string; uom: string; barcode?: string | undefined; listPrice?: number | undefined } {
  if (!variantId) {
    return { name: "—", sku: "—", uom: "EA" };
  }

  const known = KNOWN_SALES_PRODUCTS[variantId];
  if (known) {
    return {
      name: isAr ? known.nameAr : known.nameEn,
      sku: known.sku,
      uom: known.uom,
      barcode: known.barcode,
      listPrice: known.listPrice
    };
  }

  if (variantId.includes("-") && variantId.length >= 32) {
    let hash = 0;
    for (let i = 0; i < variantId.length; i++) {
      hash = (hash << 5) - hash + variantId.charCodeAt(i);
      hash |= 0;
    }
    const index = Math.abs(hash) % SEED_PRODUCT_NAMES_AR.length;
    return {
      name: isAr ? SEED_PRODUCT_NAMES_AR[index]! : SEED_PRODUCT_NAMES_EN[index]!,
      sku: `SKU-PRD-${variantId.slice(0, 6).toUpperCase()}`,
      uom: "EA",
      listPrice: 100 + (Math.abs(hash) % 200)
    };
  }

  return {
    name: variantId,
    sku: variantId,
    uom: "EA",
    listPrice: 100
  };
}
