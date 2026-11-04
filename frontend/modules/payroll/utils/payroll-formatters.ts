/**
 * Payroll domain formatters and presentation helpers.
 * Guarantees financial truth, avoids negative-zero artifacts, and resolves employee labels safely.
 */

export function formatMoneyAmount(
  amount: number | null | undefined,
  currency: string = "EGP",
  options?: { isDeduction?: boolean }
): string {
  if (amount == null || isNaN(amount)) {
    return `0 ${currency}`;
  }

  // Prevent IEEE 754 negative zero (-0) or tiny floating point noise
  if (Math.abs(amount) < 0.0001) {
    return `0 ${currency}`;
  }

  const prefix = options?.isDeduction && amount > 0 ? "-" : "";
  return `${prefix}${amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${currency}`;
}

export function formatMoneyValue(amount: number | null | undefined): string {
  if (amount == null || isNaN(amount) || Math.abs(amount) < 0.0001) {
    return "0";
  }
  return amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

export function resolveEmployeeDisplay(
  employmentId: string,
  employeesOrMap?: any,
  isAr: boolean = true
): string {
  if (!employmentId) return "—";

  // If array of employee objects
  if (Array.isArray(employeesOrMap) && employeesOrMap.length > 0) {
    const found = employeesOrMap.find(
      (e: any) => e.id === employmentId || e.employmentId === employmentId
    );
    if (found) {
      const name = found.displayName || `${found.firstName || ""} ${found.lastName || ""}`.trim() || employmentId;
      const num = found.employmentNumber || found.employeeNumber;
      return num ? `${name} (${num})` : name;
    }
  }

  // If map
  if (employeesOrMap && typeof employeesOrMap === "object" && !Array.isArray(employeesOrMap)) {
    const foundInMap = employeesOrMap[employmentId];
    if (foundInMap) {
      return `${foundInMap.displayName}${foundInMap.employmentNumber ? ` (${foundInMap.employmentNumber})` : ""}`;
    }
  }

  // Clean presentation fallback if unmapped: Avoid raw ugly UUIDs on business screens
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}/i.test(employmentId)) {
    return isAr ? `موظف #${employmentId.slice(0, 8).toUpperCase()}` : `Employee #${employmentId.slice(0, 8).toUpperCase()}`;
  }

  return employmentId;
}

export const resolveEmployeeLabel = resolveEmployeeDisplay;

export function formatRunType(type: string, isAr: boolean = true): string {
  const mapAr: Record<string, string> = {
    Regular: "دورة اعتيادية",
    OffCycle: "دورة استثنائية (Off-Cycle)",
    BonusOnly: "دورة مكافآت وحوافز",
    Retro: "أثر رجعي (Retro)",
    FinalSettlement: "تصفية مستحقات نهاية خدمة"
  };
  const mapEn: Record<string, string> = {
    Regular: "Regular Run",
    OffCycle: "Off-Cycle Run",
    BonusOnly: "Bonus & Incentives",
    Retro: "Retroactive Adjustment",
    FinalSettlement: "Final Settlement"
  };
  return (isAr ? mapAr[type] : mapEn[type]) ?? type;
}

export function formatComponentCategory(category: string, isAr: boolean = true): string {
  const mapAr: Record<string, string> = {
    Basic: "راتب أساسي",
    Allowance: "بدل ثابت",
    Deduction: "استقطاع",
    Earning: "استحقاق إضافي",
    Loan: "سلفة وقرض",
    Tax: "ضريبة",
    SocialInsurance: "تأمينات اجتماعية"
  };
  const mapEn: Record<string, string> = {
    Basic: "Basic Salary",
    Allowance: "Allowance",
    Deduction: "Deduction",
    Earning: "Earning",
    Loan: "Loan / Advance",
    Tax: "Tax",
    SocialInsurance: "Social Insurance"
  };
  return (isAr ? mapAr[category] : mapEn[category]) ?? category;
}

export function formatInputMode(mode: string, isAr: boolean = true): string {
  const mapAr: Record<string, string> = {
    Formula: "معادلة تلقائية",
    Manual: "إدخال يدوي",
    Provider: "حزمة قانونية"
  };
  const mapEn: Record<string, string> = {
    Formula: "Automated Formula",
    Manual: "Manual Entry",
    Provider: "Statutory Provider"
  };
  return (isAr ? mapAr[mode] : mapEn[mode]) ?? mode;
}

export function formatComponentDirection(direction: string, isAr: boolean = true): string {
  const mapAr: Record<string, string> = {
    Credit: "+ استحقاق",
    Debit: "- استقطاع",
    Informational: "إحصائي"
  };
  const mapEn: Record<string, string> = {
    Credit: "+ Earning",
    Debit: "- Deduction",
    Informational: "Informational"
  };
  return (isAr ? mapAr[direction] : mapEn[direction]) ?? direction;
}

export function formatLifecycleStatus(status: string, isAr: boolean = true): string {
  const mapAr: Record<string, string> = {
    Active: "نشط",
    Inactive: "غير نشط",
    Open: "مفتوحة",
    Draft: "مسودة",
    Finalized: "مقفلة",
    Closed: "مغلقة",
    Pending: "معلق",
    Approved: "معتمد",
    Cancelled: "ملغاة"
  };
  const mapEn: Record<string, string> = {
    Active: "Active",
    Inactive: "Inactive",
    Open: "Open",
    Draft: "Draft",
    Finalized: "Finalized",
    Closed: "Closed",
    Pending: "Pending",
    Approved: "Approved",
    Cancelled: "Cancelled"
  };
  return (isAr ? mapAr[status] : mapEn[status]) ?? status;
}
