export const ar = {
  language: { arabic: "العربية", english: "الإنجليزية", switchToArabic: "التبديل إلى العربية", switchToEnglish: "التبديل إلى الإنجليزية" },
  shell: {
    workspace: "مساحة العمل", legalEntity: "الكيان القانوني", branch: "الفرع", emptyWorkspace: "اختر مساحة عمل للبدء",
    updatingContext: "جارٍ تحديث السياق", signOut: "تسجيل الخروج", modules: "الوحدات والخدمات", administrator: "مسؤول النظام",
    couldNotLoadSession: "تعذر تحميل الجلسة", couldNotUpdateScope: "تعذر تحديث النطاق التنظيمي"
  },
  preferences: {
    language: "اللغة", theme: "المظهر", alternateTheme: "البديل", defaultTheme: "الافتراضي",
    density: "كثافة العرض", compact: "مدمج", comfortable: "مريح", profile: "الملف الشخصي"
  },
  actions: {
    save: "حفظ", cancel: "إلغاء", close: "إغلاق", confirm: "تأكيد", retry: "إعادة المحاولة",
    back: "رجوع", create: "إنشاء", add: "إضافة", edit: "تعديل", delete: "حذف", search: "بحث",
    filter: "تصفية", clear: "مسح", continue: "متابعة", refresh: "تحديث", yes: "نعم", no: "لا"
  },
  states: {
    loading: "جارٍ التحميل...", saving: "جارٍ الحفظ...", empty: "لا توجد بيانات للعرض",
    error: "تعذر إتمام الطلب. حاول مرة أخرى.", required: "هذا الحقل مطلوب.",
    active: "نشط", inactive: "غير نشط", enabled: "مفعّل", disabled: "معطّل"
  },
  access: {
    deniedTitle: "غير مصرح لك بالوصول", deniedDescription: "لا تملك الصلاحيات الكافية للوصول إلى هذا المورد.",
    errorTitle: "حدث خطأ غير متوقع", errorDescription: "تعذر تحميل البيانات المطلوبة.",
    shellErrorTitle: "حدث خطأ غير متوقع في واجهة النظام",
    shellErrorDescription: "حدث خلل مؤقت أثناء عرض الصفحة. يمكنك إعادة المحاولة أو العودة للرئيسية."
  },
  accessibility: { mainNavigation: "التنقل الرئيسي", closeDialog: "إغلاق النافذة" }
  ,pagination: { navigation: "التنقل بين الصفحات", total: "إجمالي السجلات:", perPage: "لكل صفحة:", previous: "السابق", next: "التالي", page: "الصفحة {{page}}" },
  components: {
    loadErrorTitle: "تعذر تحميل البيانات", loadErrorMessage: "حدث خطأ غير متوقع أثناء استرجاع البيانات من الخادم. يرجى المحاولة مرة أخرى.",
    retry: "إعادة المحاولة", loading: "جارٍ تحميل البيانات...", searchPlaceholder: "البحث...", searchField: "حقل البحث", clearSearch: "مسح البحث",
    activeFilters: "عوامل التصفية النشطة: {{count}}", resetFilters: "إعادة تعيين",
    applyFilters: "تطبيق", applyingFilters: "جارٍ التطبيق...", confirm: "تأكيد", cancel: "إلغاء", processing: "جارٍ التنفيذ...",
    fileChoose: "اختر ملفًا", fileDrop: "أو اسحب الملف وأفلته هنا", fileSelected: "الملف المحدد: {{name}}", removeFile: "إزالة الملف",
    fileTooLarge: 'حجم الملف "{{name}}" أكبر من الحد الأقصى المسموح ({{size}}).', fileBrowse: "اسحب الملف وأفلته هنا، أو انقر للتصفح",
    supportedFormats: "الصيغ المدعومة: {{formats}}", allFileTypes: "جميع أنواع الملفات مدعومة", maxFileSize: "الحد الأقصى: {{size}}", deleteFile: "حذف الملف {{name}}",
    selectOption: "اختر خيارًا...", closeDialog: "إغلاق النافذة", closeAlert: "إغلاق التنبيه", progress: "مؤشر التقدم",
    validationSummary: "يرجى تصحيح الأخطاء التالية قبل المتابعة:", unsavedChanges: "توجد تعديلات غير محفوظة. هل أنت متأكد من رغبتك في مغادرة الصفحة؟",
    showPassword: "إظهار كلمة المرور", hidePassword: "إخفاء كلمة المرور", copyValue: "نسخ القيمة", copied: "تم النسخ",
    wizardSteps: "خطوات المعالج", breadcrumbNavigation: "التنقل عبر مسار الصفحة",
    noRows: "لا توجد سجلات", noRowsFiltered: "لا توجد سجلات تطابق معايير البحث الحالية", loadingRecords: "جارٍ تحميل السجلات...",
    createdAt: "تاريخ الإنشاء:", updatedAt: "آخر تعديل:", byActor: "بواسطة {{actor}}",
    statusConfirmed: "مؤكد", statusDraft: "مسودة", statusCancelled: "ملغى", statusUnknown: "حالة غير متاحة",
    notifications: "إشعارات النظام", closeNotification: "إغلاق الإشعار",
    accountingPosted: "مرحّل محاسبياً", accountingUnposted: "غير مرحّل", approvalApproved: "معتمد إدارياً",
    approvalRejected: "مرفوض", approvalPending: "بانتظار الاعتماد", complianceValid: "مطابق ضريبياً",
    settlementPaid: "مسدد بالكامل", settlementPartial: "مسدد جزئياً", settlementUnpaid: "غير مسدد", documentLocked: "مستند مقفل"
  }
  ,grid: {
    selectAll: "تحديد الكل", searchOoo: "بحث...", blanks: "فارغ", noMatches: "لا توجد خيارات مطابقة",
    filterOoo: "تصفية...", equals: "يساوي", notEqual: "لا يساوي", lessThan: "أقل من", greaterThan: "أكبر من",
    lessThanOrEqual: "أقل من أو يساوي", greaterThanOrEqual: "أكبر من أو يساوي", inRange: "ضمن النطاق",
    contains: "يحتوي على", notContains: "لا يحتوي على", startsWith: "يبدأ بـ", endsWith: "ينتهي بـ",
    noRows: "لا توجد سجلات", page: "الصفحة", of: "من", to: "إلى", next: "التالي", previous: "السابق"
  }
} as const;
