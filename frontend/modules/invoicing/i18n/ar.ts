export const ar = {
  title: "فواتير المبيعات",
  document: {
    loading: "جارٍ تحميل بيانات الفاتورة...", loadError: "تعذر تحميل بيانات الفاتورة أو أن المستند غير موجود.", back: "العودة",
    concurrentEdit: "تم تعديل هذا المستند من قبل مستخدم آخر. يرجى إعادة تحميل الصفحة لمراجعة أحدث البيانات.", postError: "تعذر ترحيل الفاتورة إلى الحسابات.",
    postedLocked: "المستند مرحّل ومقيد محاسبياً", subtotal: "المجموع الفرعي (قبل الخصم والضريبة)", totalDiscount: "إجمالي الخصم التجاري",
    totalTax: "ضريبة القيمة المضافة (15%)", totalDue: "المبلغ الإجمالي المستحق", type: "فاتورة مبيعات ضريبية", sales: "المبيعات",
    issueCreditNote: "إصدار إشعار دائن (تصحيح)", posting: "جارٍ الترحيل...", postToAccounts: "ترحيل إلى الحسابات", customer: "العميل / الطرف التجاري",
    issueDate: "تاريخ الإصدار", dueDate: "تاريخ الاستحقاق", currency: "العملة", lineDescription: "الوصف / البند", quantity: "الكمية",
    unit: "الوحدة", unitPrice: "سعر الوحدة", discount: "الخصم", tax: "الضريبة", total: "الإجمالي", notesAndTerms: "الملاحظات والشروط",
    creditNoteTitle: "إصدار إشعار دائن (Credit Note)", creditNoteDescription: "تصحيح أو إلغاء الفاتورة المقيدة رقم {{number}}",
    creditNoteWarning: "وفقاً لقواعد الامتثال المالي وقيد اليومية، لا يمكن حذف أو تعديل الفواتير المرحلة. سيتم إنشاء إشعار دائن مرتبط لعكس القيود المحاسبية.",
    cancel: "إلغاء", continueCreditNote: "متابعة إنشاء الإشعار"
  }
} as const;
