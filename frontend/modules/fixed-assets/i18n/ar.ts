export const ar = {
  moduleTitle: "إدارة الأصول الثابتة والعهد",
  moduleSubtitle: "سجل الأصول، العمليات الميدانية والعهد، دورة استهلاك الأصول، والرسملة والمطابقة المحاسبية",
  tabs: {
    register: "سجل الأصول",
    custody: "العمليات الميدانية والعهد",
    capitalization: "الرسملة والمشاريع (CIP)",
    depreciation: "الدفاتر والاستهلاك",
    reports: "المطابقة والتقارير",
    myAssets: "أصولي وعهد العمل"
  },
  kpis: {
    totalAssets: "إجمالي الأصول المقيدة",
    grossCost: "إجمالي التكلفة التاريخية",
    accumulatedDepreciation: "مجمع الاستهلاك المتراكم",
    netBookValue: "صافي القيمة الدفترية (NBV)",
    activeCustodyCount: "الأصول المسلمة بعهدة",
    cipActiveCost: "مشاريع تحت التنفيذ (CIP)"
  },
  states: {
    Draft: "مسودة",
    UnderConstruction: "تحت التنفيذ",
    Active: "نشط / بالخدمة",
    FullyDepreciated: "مستهلك بالكامل",
    Disposed: "مستبعد",
    WrittenOff: "مشطوب"
  },
  natures: {
    Land: "أراضٍ",
    Building: "مبانٍ وإنشاءات",
    LeaseholdImprovement: "تحسينات عقارات مستأجرة",
    Vehicle: "مركبات ووسائل نقل",
    Equipment: "آلات ومعدات",
    Furniture: "أثاث وتجهيزات مكتبية",
    Tool: "عدد وأدوات",
    CIP: "مشروع تحت التنفيذ",
    Intangible: "أصول غير ملموسة / برمجيات"
  },
  tracking: {
    Individual: "أصل فردي برقم مميز",
    Grouped: "مجموعة أصول متجانسة",
    Bulk: "أصل كمي / إجمالي"
  },
  assignmentTypes: {
    Employee: "عهدة موظف",
    Department: "عهدة قسم / إدارة",
    Location: "موقع تشغيلي",
    Unassigned: "غير مسند / بالمستودع"
  },
  verificationStatuses: {
    Draft: "مسودة",
    Active: "جارية",
    Completed: "مكتملة ومقفلة",
    Cancelled: "ملغاة"
  },
  verificationResults: {
    Found: "مطابق وموجود",
    Missing: "مفقود / غير موجود",
    WrongLocation: "بموقع مختلف",
    WrongCustodian: "لدى موظف آخر",
    Damaged: "تالف / بحاجة صيانة"
  },
  depreciationStatuses: {
    Draft: "مسودة",
    Calculated: "محسوب وجاهز",
    Posted: "مرحل للأستاذ العام",
    Reversed: "معكوس بقيد عكسي"
  },
  candidateStatuses: {
    Pending: "بانتظار المراجعة",
    Reviewed: "تمت المراجعة والاعتماد",
    Allocated: "مخصص بالكامل",
    Rejected: "مرفوض / غير مؤهل"
  },
  candidateSources: {
    SupplierInvoice: "فاتورة مشتريات مورد",
    InventoryIssue: "صرف مخزني للاستخدام",
    ExpenseClaim: "مطالبة مصروفات",
    PurchasingOrder: "أمر شراء مستلم",
    Manual: "قيد يدوي معتمد"
  },
  disposalTypes: {
    Scrap: "تخريد / إتلاف",
    Loss: "فقدان",
    Theft: "سرقة",
    Sale: "بيع كأصل رأسمالي",
    Donation: "إهداء / تبرع"
  },
  actions: {
    createAsset: "تسجيل أصل جديد",
    assignAsset: "إسناد / نقل عهدة",
    returnAsset: "استرداد العهدة",
    recordHandover: "توثيق استلام العهدة",
    scanLookup: "فحص الباركود / QR",
    massTransfer: "نقل جماعي للعهد",
    addCost: "إضافة تكلفة رأسمالية",
    estimateChange: "تعديل التقديرات المحاسبية",
    impairment: "تسجيل هبوط / اضمحلال",
    revaluation: "مقترح إعادة تقييم",
    dispose: "استبعاد الأصل",
    sell: "بيع الأصل",
    reinstate: "إعادة قيد الأصل",
    newCategory: "فئة أصول جديدة",
    newLocation: "موقع جديد",
    newBook: "دفتر أصول جديد",
    addBookToAsset: "ربط دفتر بالأصل",
    prepareDepreciation: "إعداد مسير استهلاك",
    postDepreciation: "ترحيل للأستاذ العام",
    reverseDepreciation: "قيد عكسي للاستهلاك",
    newCampaign: "بدء حملة جرد",
    verifyAsset: "تسجيل نتيجة جرد",
    closeCampaign: "إغلاق حملة الجرد",
    reviewCandidate: "مراجعة المصدر",
    allocateCandidate: "تخصيص ورسملة التكلفة",
    correctAllocation: "تصحيح التخصيص",
    newCip: "مشروع جديد (CIP)",
    addCipCost: "إضافة تكلفة للمشروع",
    capitalizeCip: "رسملة المشروع بالكامل",
    splitCip: "توزيع وتجزئة المشروع",
    combineCip: "دمج مشاريع متعددة",
    splitGrouped: "تجزئة أصول مجمعة",
    exportCsv: "تصدير سجل الأصول (CSV)",
    drilldown: "سجل العمليات التفصيلي",
    refresh: "تحديث البيانات",
    close: "إغلاق",
    cancel: "إلغاء",
    confirm: "تأكيد",
    submit: "حفظ واعتماد",
    save: "حفظ",
    filter: "تصفية"
  },
  register: {
    title: "سجل الأصول والعهد",
    searchPlaceholder: "البحث برقم الأصل، الاسم، الباركود، أو السيريال...",
    filterState: "حالة الأصل",
    filterNature: "طبيعة الأصل",
    filterAll: "الكل",
    colNumber: "رقم الأصل",
    colName: "اسم الأصل",
    colCategory: "الفئة والنوع",
    colLocation: "الموقع الفعلي",
    colCustodian: "المسؤول / العهدة",
    colState: "الحالة التشغيلية",
    colQty: "الكمية",
    colCost: "التكلفة",
    colNbv: "صافي القيمة الدفترية",
    actions: "الإجراءات",
    emptyTitle: "لا توجد أصول مطابقة للبحث",
    emptyDescription: "لم يتم تسجيل أصول تتوافق مع معايير التصفية المحددة. يمكنك إضافة أصل جديد للبدء.",
    noPermissionTitle: "غير مصرح باستعراض السجل المالي",
    noPermissionDescription: "ليس لديك صلاحية استعراض القيم المالية والدفترية للأصول الثابتة."
  },
  drawer: {
    title: "الملف الشامل للأصل (360°)",
    overviewTab: "نظرة عامة والبيانات الفنية",
    booksTab: "الدفاتر والأرصدة المحاسبية",
    custodyTab: "العهدة والمواقع والتسليم",
    scheduleTab: "جدول الاستهلاك المتوقع",
    documentsTab: "المستندات والوثائق",
    timelineTab: "سجل التغييرات والتدقيق",
    drilldownTab: "حركات التكلفة والمصادر",
    identityCard: "بيانات الهوية والتصنيف",
    tagLabel: "رقم الباركود / التاج",
    serialLabel: "الرقم التسلسلي (سيريال)",
    productRefLabel: "مرجع الصنف بالكتالوج",
    acquisitionDateLabel: "تاريخ الاستحواذ / الشراء",
    capitalizationDateLabel: "تاريخ الرسملة المعتمد",
    placedInServiceLabel: "تاريخ بدء التشغيل الفعلي",
    quantityLabel: "الكمية الحالية",
    currentLocationLabel: "الموقع الحالي المعتمد",
    currentCustodianLabel: "المستلم / صاحب العهدة الحالي",
    parentAssetLabel: "الأصل الرئيسي التابع له",
    booksSummary: "الدفاتر المحاسبية المسجلة للأصل",
    noBooksDescription: "لم يتم ربط هذا الأصل بدفتر مالي حتى الآن.",
    scheduleTitle: "الاستهلاك المالي المتوقع على مدار العمر الإنتاجي",
    scheduleEmpty: "الاستهلاك غير متاح أو أن الأصل معفى من الاستهلاك (كالأراضي).",
    timelineEmpty: "لا توجد أحداث مسجلة لهذا الأصل.",
    drilldownEmpty: "لا توجد حركات تكلفة مسجلة لهذا الأصل."
  },
  custody: {
    title: "العمليات الميدانية والتحقق من العهد",
    subtitle: "إدارة المواقع التشغيلية، حركات تسليم واسترداد العهد، وحملات الجرد الفعلي",
    locationsTitle: "المواقع والمباني التشغيلية",
    campaignsTitle: "حملات الجرد والمطابقة الفعلية",
    massTransferTitle: "النقل الجماعي للعهد",
    scanTitle: "فحص الباركود ورمز الاستجابة السريعة (QR)",
    noLocations: "لم تسجل أي مواقع جغرافية أو تشغيلية بعد.",
    noCampaigns: "لا توجد حملات جرد حالية.",
    startCampaignNotice: "تتيح حملة الجرد التقاط صورة لحظية للأصول المقيدة ومطابقتها ميدانياً دون إيقاف الحركة."
  },
  capitalization: {
    title: "منصة الرسملة والمشاريع الرأسمالية (CIP)",
    subtitle: "إدارة تكاليف الاستحواذ القادمة من المشتريات والمخزون، وتجميع مشروعات الأصول تحت التنفيذ",
    candidatesTitle: "مصادر التكلفة المرشحة للرسملة",
    cipTitle: "المشاريع والأصول تحت التنفيذ (CIP)",
    noCandidates: "لا توجد مصادر تكلفة بانتظار الرسملة حالياً.",
    noCip: "لا توجد مشاريع تحت التنفيذ قيد التطوير حالياً.",
    allocateHint: "يمكن تخصيص المصدر لأصل واحد أو تجزئته على عدة أصول برصيد تراكمي متزن."
  },
  depreciation: {
    title: "دفاتر الأصول ومسيرات الاستهلاك الشهري",
    subtitle: "إدارة الدفاتر المحاسبية والضريبية، وحساب وإثبات أقساط الاستهلاك في الأستاذ العام",
    booksTitle: "دفاتر الأصول المعتمدة للمنشأة",
    runsTitle: "سجل مسيرات الاستهلاك الشهري",
    noBooks: "لم تسجل دفاتر أصول بعد.",
    noRuns: "لم ينفذ أي مسير استهلاك بعد.",
    postConfirm: "هل أنت متأكد من رغبتك في ترحيل قسط الاستهلاك للأستاذ العام؟ هذه الحركة ستنشئ قيوداً محاسبية غير قابلة للحذف.",
    reverseConfirm: "إجراء القيد العكسي سيعوض كافة القيود الدفترية المرحلة لهذا المسير مع إلزامية توثيق السبب المعتمد."
  },
  reports: {
    title: "تقارير سجل الأصول والمطابقة الدفترية",
    subtitle: "مطابقة رصيد الأصول مع حسابات الأستاذ العام، وتصدير سجل الأصول المعتمد لأغراض المراجعة والتدقيق",
    reconciliationTitle: "مطابقة الدفاتر مع الأستاذ العام (Book-to-GL)",
    registerReportTitle: "سجل الأصول الشامل بحسب التاريخ",
    exportBtn: "تصدير السجل إلى ملف (CSV)",
    asOfDateLabel: "كما في تاريخ",
    balancedStatus: "متطابق تماماً بدون فروقات",
    unbalancedStatus: "يوجد فرق بحاجة مراجعة",
    grossDiff: "فرق التكلفة التاريخية",
    depDiff: "فرق مجمع الاستهلاك"
  },
  myAssets: {
    title: "أصولي وعهد العمل المسندة لي",
    subtitle: "قائمة بجميع الأجهزة والمعدات والسيارات المسلمة بعهدتك الشخصية مع تفاصيل الحالة",
    noAssets: "لا توجد أصول مسجلة بعهدتك حالياً.",
    returnAction: "طلب تسليم / إرجاع العهدة"
  },
  modals: {
    newAsset: {
      title: "تسجيل أصل رأسمالي جديد",
      description: "إدخال بيانات الأصل مع تحديد الفئة وطبيعة الأصل والبيانات الفنية",
      nameLabel: "اسم الأصل *",
      namePlaceholder: "مثال: حاسب محمول Dell Latitude 7440",
      categoryLabel: "فئة الأصل *",
      assetTagLabel: "رقم الباركود / التاج الداخلي",
      serialLabel: "الرقم التسلسلي من المصنع (Serial)",
      quantityLabel: "الكمية المسجلة *",
      acquisitionDateLabel: "تاريخ الاستحواذ والشراء *",
      placedInServiceLabel: "تاريخ بدء التشغيل الفعلي",
      parentAssetLabel: "أصل رئيسي تابع له (اختياري)",
      descriptionLabel: "ملاحظات وتفاصيل إضافية"
    },
    assign: {
      title: "إسناد / نقل عهدة أصل",
      description: "تحديد الشخص أو الإدارة أو الموقع الجغرافي المسؤول عن تشغيل الأصل",
      typeLabel: "نوع جهة الإسناد *",
      employeeLabel: "الموظف المستلم للعهدة",
      departmentLabel: "الإدارة أو القسم",
      locationLabel: "الموقع الفعلي / الغرفة / الفرع",
      custodianLabel: "اسم المسؤول الميداني",
      effectiveFromLabel: "تاريخ بدء سريان العهدة *",
      effectiveToLabel: "تاريخ الانتهاء المتوقع (اختياري)",
      conditionLabel: "حالة الأصل الفنية عند التسليم",
      accessoriesLabel: "الملحقات والملزمات المسلمة معه",
      ackRefLabel: "رقم وثيقة التسليم / إقرار الاستلام"
    },
    return: {
      title: "استرداد عهدة أصل للمستودع",
      description: "إثبات تسليم الأصل وإنهاء عهدة الموظف وتحديد موقع التخزين الجديد",
      effectiveFromLabel: "تاريخ الاسترداد والتسليم *",
      locationLabel: "موقع التخزين / المستودع المستلم *",
      conditionLabel: "حالة الأصل عند الاستلام والفحص",
      accessoriesLabel: "الملحقات المستلمة",
      ackRefLabel: "رقم إقرار الاستلام المخزني"
    },
    dispose: {
      title: "استبعاد أصل رأسمالي",
      description: "إثبات استبعاد الأصل من السجلات المالية مع احتساب الأرباح أو الخسائر الرأسمالية",
      typeLabel: "سبب وطريقة الاستبعاد *",
      dateLabel: "تاريخ الاستبعاد المحاسبي *",
      proceedsLabel: "المتحصلات النقدية (إن وجدت)",
      directCostsLabel: "تكاليف الاستبعاد والإزالة المباشرة",
      reasonLabel: "المبرر المحاسبي للاستبعاد *",
      quantityLabel: "الكمية المستبعدة (للاستبعاد الجزئي)"
    },
    reinstate: {
      title: "إعادة قيد أصل مستبعد",
      description: "إلغاء أثر الاستبعاد وإعادة تنشيط الأصل مع إنشاء القيود العكسية المقاصة",
      dateLabel: "تاريخ إعادة القيد المعتمد *",
      reasonLabel: "سبب ومبرر إعادة القيد *"
    },
    cipCapitalize: {
      title: "رسملة مشروع تحت التنفيذ بالكامل",
      description: "تحويل المشروع الرأسمالي لأصل ثابت نشط بالخدمة مع تحديد فئته التشغيلية",
      targetCategoryLabel: "فئة الأصل النهائي بعد الإنجاز *",
      targetNameLabel: "اسم الأصل المعتمد بالخدمة",
      capitalizationDateLabel: "تاريخ الرسملة وإتمام المشروع *",
      placedInServiceLabel: "تاريخ بدء الاستخدام والتشغيل *"
    },
    prepareRun: {
      title: "إعداد مسير استهلاك دوري",
      description: "حساب أقساط الاستهلاك لجميع أصول الدفتر عن الفترة المحددة",
      bookLabel: "دفتر الأصول المعني *",
      periodStartLabel: "بداية الفترة *",
      periodEndLabel: "نهاية الفترة *"
    },
    reverseRun: {
      title: "قيد عكسي لمسير استهلاك مرحل",
      description: "إلغاء قيود الاستهلاك المرحلة للأستاذ العام لهذا المسير وتوثيق المبرر",
      reversalDateLabel: "تاريخ القيد العكسي *",
      reasonLabel: "السبب المعتمد للقيد العكسي *"
    },
    verification: {
      title: "تسجيل نتيجة جرد لأصل",
      description: "تثبيت الملاحظة الميدانية ومطابقة الموقع والعهدة",
      resultLabel: "نتيجة فحص الأصل *",
      observedLocationLabel: "الموقع الفعلي المرصود (إن كان مختلفاً)",
      notesLabel: "ملاحظات الفاحص الميداني"
    }
  }
} as const;

export const fixedAssetsAr = ar;
