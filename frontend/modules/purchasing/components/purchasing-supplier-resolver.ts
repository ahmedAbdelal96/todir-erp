export interface ResolvedSupplier {
  readonly id: string;
  readonly name: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly code: string;
  readonly taxNumber?: string | undefined;
  readonly phone?: string | undefined;
  readonly nature: "Stock" | "Service" | "Trading";
  readonly blocked?: boolean | undefined;
}

export const KNOWN_PURCHASING_SUPPLIERS: Record<string, ResolvedSupplier> = {
  "party-supplier-al-futtaim": {
    id: "party-supplier-al-futtaim",
    name: "مجموعة الفطيم للتجهيزات والمعدات",
    nameAr: "مجموعة الفطيم للتجهيزات والمعدات",
    nameEn: "Al-Futtaim Equipment Group",
    code: "SUP-FUTTAIM-01",
    taxNumber: "300022334400003",
    phone: "+966 51 234 5678",
    nature: "Trading"
  },
  "party-supplier-al-marai": {
    id: "party-supplier-al-marai",
    name: "شركة المراعي الصناعية والتوريد",
    nameAr: "شركة المراعي الصناعية والتوريد",
    nameEn: "Al Marai Industrial Supply",
    code: "SUP-MARAI-02",
    taxNumber: "300033445500003",
    phone: "+966 52 345 6789",
    nature: "Stock"
  },
  "party-supplier-delta": {
    id: "party-supplier-delta",
    name: "شركة دلتا للتوريدات الصناعية",
    nameAr: "شركة دلتا للتوريدات الصناعية",
    nameEn: "Delta Industrial Supplies Co.",
    code: "SUP-DELTA-01",
    taxNumber: "300123456700003",
    phone: "+966 11 405 1234",
    nature: "Stock"
  },
  "party-supplier-riyadh": {
    id: "party-supplier-riyadh",
    name: "مؤسسة الرياض للورق والتغليف",
    nameAr: "مؤسسة الرياض للورق والتغليف",
    nameEn: "Riyadh Paper & Packaging Est.",
    code: "SUP-RYD-02",
    taxNumber: "300765432100003",
    phone: "+966 11 234 5678",
    nature: "Stock"
  },
  "party-supplier-gulf-service": {
    id: "party-supplier-gulf-service",
    name: "شركة الخليج للخدمات التقنية والاستشارية",
    nameAr: "شركة الخليج للخدمات التقنية والاستشارية",
    nameEn: "Gulf Technical & Consulting Services",
    code: "SUP-GULF-SRV",
    taxNumber: "310987654300003",
    phone: "+966 13 888 9900",
    nature: "Service"
  },
  "party-supplier-atlas": {
    id: "party-supplier-atlas",
    name: "شركة الأطلس للمعدات المكتبية",
    nameAr: "شركة الأطلس للمعدات المكتبية",
    nameEn: "Atlas Office Equipment Co.",
    code: "SUP-ATLAS-04",
    taxNumber: "300334455600003",
    phone: "+966 12 600 4433",
    nature: "Trading"
  },
  "party-supplier-blocked": {
    id: "party-supplier-blocked",
    name: "شركة التوريد المحظورة ذ.م.م",
    nameAr: "شركة التوريد المحظورة ذ.م.م",
    nameEn: "Blocked Supplies LLC",
    code: "SUP-BLOCKED-99",
    taxNumber: "300999999900003",
    phone: "+966 11 999 0000",
    nature: "Trading",
    blocked: true
  }
};

export function resolvePurchasingSupplier(partyId?: string | null, isArabic = true): ResolvedSupplier {
  if (!partyId) {
    return {
      id: "unknown",
      name: isArabic ? "مورد غير محدد" : "Unspecified Supplier",
      nameAr: "مورد غير محدد",
      nameEn: "Unspecified Supplier",
      code: "SUP-NONE",
      nature: "Trading"
    };
  }

  if (KNOWN_PURCHASING_SUPPLIERS[partyId]) {
    const s = KNOWN_PURCHASING_SUPPLIERS[partyId];
    return {
      ...s,
      name: isArabic ? s.nameAr : s.nameEn
    };
  }

  // Derive stable business name without leaking raw UUID
  const clean = partyId.replace(/-/g, "").toUpperCase();
  const shortCode = clean.substring(clean.length - 4);
  const derivedCode = `SUP-${shortCode}`;
  const derivedNameAr = `مورد معتمد (${derivedCode})`;
  const derivedNameEn = `Approved Supplier (${derivedCode})`;

  return {
    id: partyId,
    name: isArabic ? derivedNameAr : derivedNameEn,
    nameAr: derivedNameAr,
    nameEn: derivedNameEn,
    code: derivedCode,
    nature: "Trading"
  };
}
