export const ar = {
  title: "المحاسبة العامة",
  journal: {
    loading: "جارٍ تحميل قيد اليومية...", error: "تعذر تحميل بيانات قيد اليومية أو أن القيد غير موجود.", back: "العودة",
    accountCodeName: "رمز الحساب والاسم", memo: "البيان / الشرح", debit: "مدين", credit: "دائن", costCenter: "مركز التكلفة",
    documentType: "قيد يومية يدوي", generalLedger: "المحاسبة العامة", entries: "قيود اليومية", postPending: "جارٍ الترحيل...", post: "ترحيل القيد",
    entryDate: "تاريخ القيد", period: "الفترة المحاسبية", balanceStatus: "حالة التوازن", balanced: "القيد متوازن", unbalanced: "القيد غير متوازن",
    addLine: "إضافة سطر محاسبي جديد", totalDebit: "إجمالي المدين", totalCredit: "إجمالي الدائن", difference: "الفارق المحاسبي", description: "البيان العام للقيد",
    postedLocked: "القيد مرحل إلى دفتر الأستاذ العام", periodClosed: "الفترة المالية مغلقة"
  },
  accountPicker: { label: "الحساب المالي", searching: "جارٍ البحث...", placeholder: "ابحث برمز الحساب أو الاسم..." }
} as const;
