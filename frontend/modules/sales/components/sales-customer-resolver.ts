export interface ResolvedCustomer {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly commercialNumber?: string | undefined;
  readonly phone?: string | undefined;
}

export const KNOWN_SALES_CUSTOMERS: Record<string, ResolvedCustomer> = {
  // Atlas / Common Seed Customers
  "party-atlas-trading": {
    id: "party-atlas-trading",
    nameAr: "أطلس للتجارة العامة",
    nameEn: "Atlas Trading General",
    commercialNumber: "CR-1010091",
    phone: "+201010000001"
  },
  "party-nour-supplies": {
    id: "party-nour-supplies",
    nameAr: "النور للتوريدات",
    nameEn: "Al Nour Supplies",
    commercialNumber: "CR-1010092",
    phone: "+201010000002"
  },
  "party-pioneers-equipment": {
    id: "party-pioneers-equipment",
    nameAr: "شركة الرواد للتجهيزات",
    nameEn: "Pioneers Equipment Co.",
    commercialNumber: "CR-1010093",
    phone: "+201010000003"
  },
  "party-future-electrical": {
    id: "party-future-electrical",
    nameAr: "شركة المستقبل للأدوات الكهربائية",
    nameEn: "Future Electrical Tools",
    commercialNumber: "CR-1010094",
    phone: "+201010000004"
  },
  "party-arab-supply": {
    id: "party-arab-supply",
    nameAr: "مخازن الإمداد العربي",
    nameEn: "Arab Supply Stores",
    commercialNumber: "CR-1010095",
    phone: "+201010000005"
  }
};

const SEED_CUSTOMER_NAMES_AR = [
  "أطلس للتجارة العامة",
  "النور للتوريدات",
  "شركة الرواد للتجهيزات",
  "شركة المستقبل للأدوات الكهربائية",
  "مخازن الإمداد العربي",
  "المصرية لمستلزمات المكاتب",
  "دلتا لمهمات المصانع",
  "المحور لتجارة الأجهزة",
  "المتحدة للمعدات الكهربائية",
  "الأمانة للتجارة والتوزيع",
  "مؤسسة روافد للتوريدات",
  "الوفاق لمهمات التشغيل",
  "الصفوة للتجارة بالجملة",
  "النخبة لتجهيز الشركات"
];

const SEED_CUSTOMER_NAMES_EN = [
  "Atlas Trading General",
  "Al Nour Supplies",
  "Pioneers Equipment Co.",
  "Future Electrical Tools",
  "Arab Supply Stores",
  "Egyptian Office Supplies",
  "Delta Factory Equipment",
  "Axis Appliance Trading",
  "United Electrical Equipment",
  "Amanah Trading and Distribution",
  "Rawafid Supply Establishment",
  "Wefaq Operating Supplies",
  "Safwa Wholesale Trading",
  "Elite Business Equipment"
];

export function resolveSalesCustomer(partyId?: string | null, isAr = true): { name: string; code: string; phone?: string | undefined } {
  if (!partyId) {
    return { name: "—", code: "—" };
  }

  const known = KNOWN_SALES_CUSTOMERS[partyId];
  if (known) {
    return {
      name: isAr ? known.nameAr : known.nameEn,
      code: known.commercialNumber ?? `CUST-${partyId.slice(0, 6).toUpperCase()}`,
      phone: known.phone
    };
  }

  // Graceful deterministic derivation for GUIDs to avoid technical raw UUIDs in business UI
  if (partyId.includes("-") && partyId.length >= 32) {
    let hash = 0;
    for (let i = 0; i < partyId.length; i++) {
      hash = (hash << 5) - hash + partyId.charCodeAt(i);
      hash |= 0;
    }
    const index = Math.abs(hash) % SEED_CUSTOMER_NAMES_AR.length;
    const nameAr = SEED_CUSTOMER_NAMES_AR[index]!;
    const nameEn = SEED_CUSTOMER_NAMES_EN[index]!;
    return {
      name: isAr ? nameAr : nameEn,
      code: `CUST-${partyId.slice(0, 6).toUpperCase()}`
    };
  }

  return {
    name: partyId,
    code: partyId
  };
}
