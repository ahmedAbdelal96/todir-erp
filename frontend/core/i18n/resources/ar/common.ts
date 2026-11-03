export const ar = {
  language: { arabic: "العربية", english: "الإنجليزية", switchToArabic: "التبديل إلى العربية", switchToEnglish: "التبديل إلى الإنجليزية" },
  shell: {
    workspace: "مساحة العمل", legalEntity: "الكيان القانوني", branch: "الفرع", emptyWorkspace: "اختر مساحة عمل للبدء",
    updatingContext: "جارٍ تحديث السياق", signOut: "تسجيل الخروج", modules: "الوحدات والخدمات", administrator: "مسؤول النظام",
    collapseSidebar: "عرض القائمة الجانبية كأيقونات فقط", expandSidebar: "إظهار أسماء الوحدات في القائمة الجانبية",
    enterFullscreen: "عرض النظام بملء الشاشة", exitFullscreen: "الخروج من وضع ملء الشاشة",
    favorites: "المفضلة", recentPages: "آخر الصفحات", addFavorite: "إضافة {{name}} إلى المفضلة", removeFavorite: "إزالة {{name}} من المفضلة",
    navigationPreferencesSaveFailed: "تعذر حفظ المفضلة. حاول مرة أخرى.",
    displayPreferencesSaveFailed: "تعذر حفظ تفضيلات العرض على حسابك.",
    couldNotLoadSession: "تعذر تحميل الجلسة", couldNotUpdateScope: "تعذر تحديث النطاق التنظيمي"
  },
  preferences: {
    language: "اللغة", theme: "المظهر", alternateTheme: "داكن", defaultTheme: "الافتراضي",
    density: "كثافة العرض", compact: "مدمج", comfortable: "مريح", profile: "الملف الشخصي"
  },
  shortcuts: {
    title: "اختصارات لوحة المفاتيح", description: "استخدم الاختصارات للوصول إلى الأدوات الأساسية بسرعة.",
    openSearch: "فتح البحث السريع", toggleSidebar: "طيّ أو توسيع القائمة الجانبية", openHelp: "عرض هذه الاختصارات"
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
    settlementPaid: "مسدد بالكامل", settlementPartial: "مسدد جزئياً", settlementUnpaid: "غير مسدد", documentLocked: "مستند مقفل",
    createNewOption: "إنشاء {{name}} جديد...", noOptionsFound: "لا توجد نتائج مطابقة"
  }
  ,grid: {
    selectAll: "تحديد الكل", searchOoo: "بحث...", blanks: "فارغ", noMatches: "لا توجد خيارات مطابقة",
    filterOoo: "تصفية...", equals: "يساوي", notEqual: "لا يساوي", lessThan: "أقل من", greaterThan: "أكبر من",
    lessThanOrEqual: "أقل من أو يساوي", greaterThanOrEqual: "أكبر من أو يساوي", inRange: "ضمن النطاق",
    contains: "يحتوي على", notContains: "لا يحتوي على", startsWith: "يبدأ بـ", endsWith: "ينتهي بـ",
    noRows: "لا توجد سجلات", page: "الصفحة", of: "من", to: "إلى", next: "التالي", previous: "السابق"
  },
  pageHelp: {
    triggerAriaLabel: "دليل ومساعدة الصفحة",
    triggerTooltip: "دليل الاستخدام والمساعدة",
    defaultTitle: "دليل استخدام ومساعدة الصفحة",
    defaultSubtitle: "إرشادات وتوضيحات لمساعدتك في إنجاز مهامك اليومية بسهولة وسرعة",
    purposeTitle: "ما الهدف من هذه الصفحة؟",
    workflowTitle: "خطوات الاستخدام وطريقة العمل",
    conceptsTitle: "أهم المفاهيم والمصطلحات في هذه الشاشة",
    proTipsTitle: "نصائح وإرشادات لتسريع عملك",
    faqsTitle: "الأسئلة الشائعة وتفادي الأخطاء",
    close: "فهمت، إغلاق الدليل"
  },
  commandPalette: {
    title: "لوحة الأوامر والتنقل السريع",
    placeholder: "البحث في الشاشات، العمليات السريعة، أو الاختصارات... (Ctrl + K)",
    noResults: "لا توجد نتائج مطابقة لبحثك",
    openAria: "البحث السريع والأوامر",
    triggerPlaceholder: "البحث في النظام والأوامر...",
    categoryActions: "إجراءات سريعة",
    categoryNavigation: "شاشات ووحدات النظام",
    categoryRecent: "سجلات وعمليات حديثة",
    categoryRecords: "سجلات وبيانات",
    badgeRecent: "سابق",
    navigate: "للتنقل",
    select: "للاختيار",
    dismiss: "للإغلاق",
    keyEnter: "Enter",
    keyEsc: "Esc",
    shortcutCtrl: "Ctrl",
    shortcutK: "K",
    spotlightBrand: "TODIR Spotlight",
    subtitleCustomerInvoices: "إدارة وإنشاء فواتير العملاء",
    subtitleSupplierInvoices: "إدارة وتسجيل فواتير الموردين",
    subtitleCatalog: "إدارة المنتجات والخدمات والأسعار",
    subtitleDirectory: "دليل العملاء والموردين والجهات",
    subtitleSubledger: "تسوية ومطابقة الحسابات المفتوحة",
    subtitleFinbase: "الدليل المحاسبي وشجرة الحسابات",
    subtitleTax: "الإقرارات والضرائب والامتثال",
    actionNewCustomerInvoice: "إنشاء فاتورة مبيعات جديدة",
    actionNewSupplierInvoice: "إنشاء فاتورة مشتريات جديدة",
    actionNewProduct: "إضافة منتج جديد للكتالوج",
    actionNewParty: "إضافة عميل أو مورد جديد (دليل الجهات)",
    actionOpenSubledger: "تسوية البنود والمطالبات المفتوحة",
    actionChartOfAccounts: "دليل الحسابات وشجرة القيود",
    actionTaxDeclarations: "الإقرارات الضريبية والقيمة المضافة"
  },
  draftRecovery: {
    bannerTitle: "تم العثور على مسودة محفوظة تلقائياً",
    bannerDescription: "توجد بيانات غير محفوظة تم حفظها تلقائياً قبل مغادرتك السابقة.",
    restoreButton: "استعادة المسودة",
    discardButton: "تجاهل ومسح",
    restoredNotice: "تمت استعادة المسودة بنجاح",
    discardedNotice: "تم مسح المسودة المؤقتة",
    unsavedWarning: "توجد تعديلات غير محفوظة. هل أنت متأكد من مغادرة هذه الصفحة؟"
  },
  batchActions: {
    selectedCount: "تم تحديد {{count}} عنصر",
    clearSelection: "إلغاء التحديد",
    exportSelected: "تصدير المحدد",
    printSelected: "طباعة مجمعة",
    deleteSelected: "حذف المحدد",
    changeStatus: "تغيير الحالة"
  },
  undoToast: {
    undoAction: "تراجع",
    actionSuccess: "تم تنفيذ العملية بنجاح",
    actionUndone: "تم التراجع عن العملية بنجاح"
  },
  quickPeek: {
    title: "معاينة سريعة",
    closeAria: "إغلاق المعاينة السريعة",
    loading: "جارٍ تحميل تفاصيل المعاينة...",
    empty: "لا توجد تفاصيل إضافية لعرضها",
    openFullRecord: "فتح السجل بالكامل"
  },
  networkStatus: {
    offlineTitle: "لا يوجد اتصال بالإنترنت",
    offlineMessage: "بياناتك المدخلة محفوظة محلياً بأمان. سيتم استئناف الاتصال تلقائياً فور عودة الشبكة.",
    onlineTitle: "تمت استعادة الاتصال",
    onlineMessage: "تمت إعادة الاتصال بالنظام بنجاح وتحديث البيانات."
  }
} as const;
