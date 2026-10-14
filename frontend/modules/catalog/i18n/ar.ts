export const ar = {
  title: "الكتالوج",
  productList: {
    productService: "المنتج / الخدمة", type: "النوع", service: "خدمة", goods: "بضاعة", category: "الفئة", brand: "العلامة", unit: "الوحدة",
    status: "الحالة", active: "نشط", archived: "مؤرشف", pageTitle: "كتالوج المنتجات والخدمات",
    subtitle: "هوية موحدة للمنتجات والخدمات المستخدمة في المبيعات والمشتريات والمخزون",
    breadcrumbCatalog: "الكتالوج", breadcrumbProducts: "المنتجات والخدمات", add: "إضافة منتج أو خدمة",
    searchLabel: "البحث في المنتجات", searchPlaceholder: "بحث بالاسم أو SKU أو باركود...", typeFilter: "نوع المنتج",
    allTypes: "كل الأنواع", goodsFilter: "البضائع", servicesFilter: "الخدمات", loadError: "تعذر تحميل كتالوج المنتجات",
    loadErrorDescription: "حدث خطأ أثناء جلب قائمة المنتجات والخدمات من الخادم.", emptyTitle: "لا توجد منتجات أو خدمات حتى الآن",
    noResults: "لم يتم العثور على منتجات تطابق معايير البحث الحالية.", emptyDescription: "ابدأ بإضافة أول منتج أو خدمة في كتالوج المؤسسة.",
    addDescription: "أنشئ الهوية ووحدة الأساس بسرعة؛ التفاصيل التجارية مملوكة لموديولات أخرى."
  }
  ,productPicker: { label: "المنتج", loading: "جارٍ التحميل...", placeholder: "ابحث عن المنتج أو الرمز..." },
  productForm: {
    identity: "الهوية الأساسية", identityDescription: "كتالوج المنتجات مسؤول عن هوية الصنف؛ أما التسعير والضرائب والمخزون فتديرها وحدات أخرى.",
    name: "اسم المنتج / الخدمة", nature: "النوع", goods: "منتج / بضاعة", service: "خدمة", sku: "SKU (اختياري)", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية",
    classification: "التصنيف والوحدة", serviceUnitDescription: "تستخدم الخدمة وحدة قياس مثل الساعة أو الزيارة دون افتراض وجود مخزون.",
    baseUnitDescription: "تعبر وحدة الأساس عن الكمية التي ستستخدمها الوحدات اللاحقة.", category: "الفئة", noCategory: "بدون فئة",
    brand: "العلامة التجارية", noBrand: "بدون علامة", baseUnit: "وحدة الأساس", chooseUnit: "اختر الوحدة",
    save: "حفظ المنتج", cancel: "إلغاء", nameRequired: "اسم المنتج مطلوب", baseUnitRequired: "وحدة الأساس مطلوبة"
  },
  productRecord: {
    loading: "جارٍ تحميل بيانات المنتج...", notFound: "تعذر العثور على بيانات المنتج المطلوب.", backToCatalog: "العودة للكتالوج",
    nature: "طبيعة الصنف", serviceNonStock: "خدمة غير مخزنية", goodsInventory: "بضاعة ومخزون", noInventoryTracking: "لا تتطلب تتبع رصيد مستودعي",
    inventoryMovements: "تخضع لحركات وجرد المستودعات", productStatus: "حالة المنتج", activeForSale: "نشط ومتاح للبيع", archived: "مؤرشف",
    availableForTransactions: "يمكن إدراجه بالفواتير وأوامر الشراء", stoppedForNewTransactions: "متوقف عن العمليات الجديدة", baseUnit: "وحدة القياس الأساسية",
    tradingUnit: "وحدة التسعير والتداول المعتمدة", barcodes: "الباركودات المسجلة", barcodeCount: "{{count}} باركود", none: "لا يوجد",
    scanCodes: "أكواد المسح الضوئي المرتبطة", edit: "تعديل المنتج", archive: "أرشفة الصنف", unarchive: "إلغاء الأرشفة", back: "رجوع",
    identity: "بيانات التعريف والتسمية", identityDescription: "الأسماء التجارية والرموز المميزة للصنف", displayName: "اسم العرض الأساسي",
    sku: "رمز التخزين التعريفي (SKU)", itemType: "نوع الصنف", itemStatus: "حالة الصنف", stockGoods: "بضاعة مخزنية", active: "نشط ومتاح", classification: "التصنيف والعلامة التجارية",
    classificationDescription: "المجموعة السلعية والشركات المصنعة ووحدات القياس", uncategorized: "غير مصنف", unitOfMeasure: "وحدة القياس المعتمدة (UoM)",
    linkedBarcodes: "الباركودات المرتبطة", barcodeDescription: "الأكواد المستخدمة في نقاط البيع وماسحات الباركود", noBarcodes: "لم يتم تعيين أي باركود لهذا الصنف حتى الآن."
  }
} as const;
