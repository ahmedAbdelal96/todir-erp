/**
 * Centralized Arabic translations and formatters for technical enums,
 * diagnostic codes, and system messages in the Accounting module.
 * Eliminates technical leaks and raw English strings in user-facing views.
 */

export const HEALTH_CODE_LABELS: Record<string, string> = {
  "ledger.readiness": "جاهزية المحرك المحاسبي وقواعد التوجيه",
  "ledger.account_mapping_missing": "نقص في ربط الحسابات المحاسبية الإلزامية",
  "ledger.inventory_configuration_incomplete": "تهيئة حسابات مراقبة وتقييم المخزون غير مكتملة",
  "accounting.go_live": "حالة بدء التشغيل المالي المعتمد",
  "statement.ProfitAndLoss": "قائمة الأرباح والخسائر (الدخل)",
  "statement.BalanceSheet": "قائمة المركز المالي (الميزانية العمومية)",
  "statement.unmapped_accounts": "اكتمال توجيه كافة الحسابات بالقوائم المالية",
  "reconciliation.ar": "مطابقة سجلات العملاء والذمم المدينة (AR)",
  "reconciliation.ap": "مطابقة سجلات الموردين والذمم الدائنة (AP)",
  "reconciliation.inventory": "مطابقة تقييم المخزون والمستودعات",
  "control_balance_difference": "فارق بين رصيد الأستاذ العام والسجل المساعد",
  "control_context_missing": "سياق حساب المراقبة غير مكتمل",
  "control_currency_mismatch": "عدم تطابق عملة حساب المراقبة",
  "ledger.period": "جاهزية الفترة المحاسبية للإقفال",
  "trial_balance.balanced": "توازن ميزان المراجعة (المدين = الدائن)",
  "inventory_capability_unavailable": "خدمة المخزون غير مفعلة حالياً"
};

export const SEVERITY_LABELS: Record<string, string> = {
  Blocking: "عائق مانع",
  Attention: "تنبيه رقابي",
  Info: "معلومات سليمة",
  Warning: "تحذير"
};

export const STATUS_LABELS: Record<string, string> = {
  Blocked: "محظور / غير جاهز",
  Ready: "جاهز للاستخدام",
  Approved: "معتمد ونشط",
  Reconciled: "مطابق وسليم",
  Difference: "يوجد فارق تسوية",
  NotReady: "غير مهيأ بعد",
  NotApplicable: "غير مطبق",
  Open: "قيد المراجعة",
  Resolved: "تمت المعالجة",
  Pass: "سليم ومجتاز",
  Fail: "غير مجتاز / بحاجة لمعالجة",
  SoftClosed: "إغلاق مبدئي",
  Closed: "مقفل نهائياً",
  Preview: "معاينة مسودة",
  Previewed: "معاينة مسودة",
  Posted: "مرحل ومعتمد",
  Reversed: "تم العكس بالكامل",
  Healthy: "سليم ومتطابق"
};

export const DIRECTION_LABELS: Record<string, string> = {
  AR: "الذمم المدينة (العملاء)",
  Receivable: "ذمم مدينة (عملاء)",
  AP: "الذمم الدائنة (الموردين)",
  Payable: "ذمم دائنة (موردين)",
  Inventory: "تقييم المخزون والمستودعات",
  UnrealizedGain: "أرباح فروق صرف غير محققة",
  UnrealizedLoss: "خسائر فروق صرف غير محققة"
};

export const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Asset: "أصول",
  Liability: "التزامات",
  Equity: "حقوق ملكية",
  Revenue: "إيرادات",
  Expense: "مصروفات",
  Cash: "نقدية وما في حكمها",
  Bank: "حسابات بنكية",
  MappedAccounts: "حسابات مربوطة",
  Subtotal: "مجموع فرعي",
  Arithmetic: "معادلة حسابية",
  Formula: "معادلة تجميعية",
  Derived: "بند مشتق"
};

export function formatHealthCode(code: string): string {
  if (HEALTH_CODE_LABELS[code]) return HEALTH_CODE_LABELS[code];
  const cleaned = code.replace(/^(reconciliation|statement|ledger)\./, "");
  return HEALTH_CODE_LABELS[cleaned] || cleaned.replace(/_/g, " ");
}

export function formatSeverity(severity: string): string {
  return SEVERITY_LABELS[severity] || severity;
}

export function formatStatus(status: string): string {
  return STATUS_LABELS[status] || status;
}

export function formatDirection(direction: string): string {
  return DIRECTION_LABELS[direction] || direction;
}

export function formatAccountType(type: string): string {
  return ACCOUNT_TYPE_LABELS[type] || type;
}

export function formatDiagnosticMessage(message: string): string {
  if (!message) return "";

  // Pattern replacements for common backend messages
  if (/Statement definition (.*?) version (\d+) is active/i.test(message)) {
    const match = message.match(/Statement definition (.*?) version (\d+) is active/i);
    const kind = match?.[1]?.toLowerCase().includes("profit") ? "قائمة الأرباح والخسائر" : "قائمة المركز المالي";
    const ver = match?.[2] ?? "1";
    return `${kind}: نموذج العرض المالي الإصدار ${ver} معتمد ونشط`;
  }

  if (/Every posted account is mapped to its required financial statement/i.test(message)) {
    return "كافة الحسابات المرحلة مربوطة بدقة بالقوائم المالية المعتمدة";
  }

  if (/No differences found/i.test(message)) {
    return "الأرصدة متطابقة تماماً ولا توجد أية فروق تسوية";
  }

  if (/Account mapping version (\d+) is active/i.test(message)) {
    const match = message.match(/Account mapping version (\d+) is active/i);
    return `جدول توجيه الحسابات الإصدار ${match?.[1] ?? "1"} معتمد ومفعل`;
  }

  if (/Accounting go-live is approved/i.test(message)) {
    return "بدء التشغيل المحاسبي المالي معتمد ونشط";
  }

  // Handle comma-separated codes like "ledger.account_mapping_missing, ledger.inventory_configuration_incomplete"
  if (message.includes("ledger.") || message.includes("reconciliation.")) {
    const parts = message.split(",").map(p => p.trim());
    const translatedParts = parts.map(p => HEALTH_CODE_LABELS[p] || p);
    return translatedParts.join(" • ");
  }

  return message;
}
