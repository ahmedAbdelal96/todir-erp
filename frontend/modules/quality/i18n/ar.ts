/**
 * Module 27 Quality Management â€” Authoritative Arabic Localization (ar)
 * Fully compliant with TODIR Arabic Market Vocabulary Charter (Zero Literal Machine Translation).
 */

export const qualityAr = {
  navigation: {
    quality: "إدارة الجودة الشاملة",
    overview: "لوحة التحكم والجودة",
    inspections: "فحوصات الجودة والتشغيل",
    plans: "خطط ومواصفات الجودة",
    characteristics: "معايير وخصائص الفحص",
    samplingPlans: "خطط سحب العينات",
    restrictions: "حظر واحتجاز الجودة",
    dispositions: "القرارات الفنية والتصرف",
    issues: "سجل مشاكل الجودة",
    ncrs: "تقارير عدم المطابقة (NCR)",
    capas: "الإجراءات التصحيحية والوقائية (CAPA)",
    supplierQuality: "جودة الموردين و(SCAR)",
    certificates: "شهادات التحليل والمطابقة (COA)"
  },

  tabs: {
    overview: "لوحة التحكم وأولويات العمل",
    inspections: "فحوصات الجودة",
    plans: "خطط ومعايير الجودة",
    characteristics: "خصائص الفحص والمواصفات",
    samplingPlans: "خطط سحب العينات",
    restrictions: "الحظر واحتجاز الجودة (Holds)",
    dispositions: "القرارات الفنية (Dispositions)",
    issues: "مشاكل الجودة",
    ncrs: "حالات عدم المطابقة (NCR)",
    capas: "الإجراءات التصحيحية والوقائية (CAPA)",
    supplierQuality: "جودة الموردين و(SCAR)",
    certificates: "شهادات التحليل والمطابقة (COA)"
  },

  kpi: {
    pendingInspections: "فحوصات بانتظار التنفيذ",
    failedInspections: "فحوصات غير مطابقة",
    activeHolds: "شحنات محتجزة ومحظورة",
    pendingDispositions: "قرارات بانتظار الاعتماد/التنفيذ",
    openNcrs: "حالات عدم مطابقة مفتوحة",
    openCapas: "إجراءات تصحيحية قيد المتابعة",
    overdueScars: "مطالبات موردين متأخرة"
  },

  status: {
    Draft: "مسودة",
    Pending: "قيد الانتظار",
    InProgress: "قيد التنفيذ",
    Completed: "مكتمل",
    Passed: "مطابق ومقبول",
    Failed: "غير مطابق (مرفوض)",
    Concession: "قبول بتجاوز/استثناء",
    Active: "نشط وساري",
    Released: "مُفرَج عنه",
    NotRequired: "غير مطلوب",
    Executing: "قيد التنفيذ في المخزون",
    Confirmed: "مؤكد ومنفذ",
    Unknown: "معلق (بحاجة لمطابقة)",
    Open: "مفتوح",
    UnderInvestigation: "قيد التحقيق",
    Resolved: "تمت المعالجة",
    Closed: "مغلق",
    Investigating: "قيد البحث والتحليل",
    ActionPending: "بانتظار تنفيذ الإجراء",
    UnderReview: "قيد مراجعة الفعالية",
    Ineffective: "غير فعال (معاد فتحه)",
    Low: "منخفض المخاطر",
    Medium: "متوسط المخاطر",
    High: "عالي المخاطر",
    Critical: "حرج وشديد الخطورة",
    Issued: "صادر للمورد",
    ResponsePending: "بانتظار رد المورد",
    Accepted: "مقبول ومعتمد",
    Rejected: "مرفوض ومردود",
    Revoked: "ملغى / مسحوب",
    Superseded: "مستبدل بنسخة أحدث"
  },

  eventTypes: {
    Receiving: "استلام بضاعة (مشتريات)",
    InProcess: "أثناء التشغيل (إنتاج مرحلي)",
    Final: "فحص نهائي (منتج تام الصنع)",
    RoutingOperation: "عملية تشغيلية محددة",
    Shipping: "شحن وتسليم عملاء",
    Periodic: "فحص دوري / مخزني"
  },

  dataTypes: {
    Numeric: "قيمة رقمية قياسية",
    Text: "نص / ملاحظة فنية",
    Boolean: "مطابق / غير مطابق (نعم/لا)"
  },

  samplingMethods: {
    FixedSize: "حجم عينة ثابت",
    Percentage: "نسبة مئوية من حجم الدفعة",
    AqlStandard: "المعيار العسكري (ISO 2859 / AQL)"
  },

  decisions: {
    UseAsIs: "قبول للاستخدام بحالته (Use As Is)",
    Rework: "إعادة تشغيل وتصنيع (Rework)",
    Scrap: "تخريد وإتلاف نهائي (Scrap)",
    ReturnToSupplier: "رد مرتجع للمورد (Return to Supplier)",
    Regrade: "تخفيض الرتبة والتصنيف (Regrade)"
  },

  actions: {
    createPlan: "إنشاء خطة جودة جديدة",
    createVersion: "إصدار نسخة جديدة",
    activate: "تفعيل واعتماد النسخة",
    addApplicability: "ربط نطاق التطبيق",
    createCharacteristic: "إضافة خاصية فحص",
    createSpecification: "إنشاء مواصفة قياسية",
    addPlanCharacteristic: "إضافة خاصية للخطة",
    createSamplingPlan: "إنشاء خطة عينات",
    createInspection: "إنشاء أمر فحص جديد",
    enterResults: "تسجيل نتائج القياس",
    completeInspection: "إتمام واعتماد الفحص",
    reinspect: "إعادة فحص (Reinspection)",
    createRestriction: "حظر واحتجاز الدفعة",
    releaseRestriction: "إفراج مخزني (جزئي/كلي)",
    createDisposition: "إصدار قرار فني (Disposition)",
    approveDisposition: "اعتماد القرار الفني",
    executeDisposition: "تنفيذ أثر القرار المخزني",
    reconcileDisposition: "مطابقة وتأكيد الأثر",
    createIssue: "تسجيل مشكلة جودة",
    createNcr: "فتح تقرير عدم مطابقة (NCR)",
    addContainment: "إضافة إجراء احتواء فوري",
    createCapa: "فتح طلب CAPA جديد",
    addAction: "تكليف بإجراء تصحيحي",
    completeAction: "تأكيد إنجاز الإجراء",
    addRootCause: "توثيق جذر المشكلة",
    recordEffectiveness: "تقييم فعالية الإجراء",
    saveSupplierProfile: "حفظ سياسة جودة المورد",
    createScar: "إصدار طلب SCAR للمورد",
    submitScarResponse: "تسجيل رد المورد",
    reviewScar: "مراجعة واعتماد رد المورد",
    issueCertificate: "إصدار شهادة جودة (COA/COC)",
    printCertificate: "طباعة الشهادة الرسمية",
    filter: "تصفية النتائج",
    clearFilters: "إعادة ضبط",
    refresh: "تحديث البيانات",
    cancel: "إلغاء",
    save: "حفظ",
    saving: "جاري الحفظ...",
    completing: "جاري الإتمام...",
    complete: "إتمام",
    submitting: "جاري الإرسال...",
    submit: "إرسال",
    confirming: "جاري التأكيد...",
    confirm: "تأكيد",
    issuing: "جاري الإصدار...",
    close: "إغلاق",
    viewDetails: "عرض التفاصيل",
    openWorkspace: "فتح ورشة الفحص"
  },

  fields: {
    legalEntity: "الكيان القانوني",
    code: "الكود التعريفي",
    name: "الاسم والتوصيف",
    status: "الحالة التشغيلية",
    eventType: "نوع حدث الفحص",
    lotQuantity: "كمية الدفعة",
    lotReference: "رقم تشغيلة/دفعة المورد (Lot)",
    sampleSize: "حجم العينة المطلوب",
    sampledQuantity: "الكمية المفحوصة فعلياً",
    defectCount: "عدد العيوب المرصودة",
    conformingCount: "الكمية السليمة والمطابقة",
    nonConformingCount: "الكمية غير المطابقة",
    evaluation: "التقييم الفني النهائي",
    reinspectionCount: "مرات إعادة الفحص",
    holdReason: "سبب الاحتجاز المخزني",
    initialQuantity: "الكمية المحتجزة أصلاً",
    releasedQuantity: "الكمية المفرج عنها",
    remainingQuantity: "الكمية المتبقية بالحظر",
    targetWarehouse: "المستودع المستهدف",
    targetLocation: "الموقع/الرف المخزني",
    reworkOrderNumber: "أمر إعادة التشغيل",
    scrapReasonCode: "كود سبب التخريد والإهلاك",
    supplierRmaNumber: "رقم إذن إرجاع المورد (RMA)",
    notes: "ملاحظات إيضاحية"
  },

  severity: {
    minor: "بسيط (Minor)",
    major: "رئيسي (Major)",
    critical: "حرج وشديد الخطورة (Critical)"
  },

  risk: {
    low: "منخفض (Low)",
    medium: "متوسط (Medium)",
    high: "مرتفع (High)",
    critical: "حرج (Critical)"
  },

  issues: {
    title: "عنوان المشكلة",
    titlePlaceholder: "مثال: رصد شروخ شعرية في دفعات الصب",
    issueType: "نوع ومصدر المشكلة",
    typeReceiving: "عيب استلام فحص بضاعة",
    typeInProcess: "عيب أثناء التشغيل والتجميع",
    typeCustomerComplaint: "شكوى عميل ورديئة جودة",
    typeAuditFinding: "ملاحظة تدقيق جودة داخلي/خارجي",
    description: "الشرح التفصيلي للواقعة الفنية",
    severity: "مستوى الخطورة الفنية",
    inspectionId: "رقم أمر الفحص المرتبط (اختياري)",
    supplierPartyId: "معرف المورد المعني (اختياري)",
    optionalReference: "اختياري — اترك فارغاً إن لم ينطبق",
    createTitle: "تسجيل مشكلة جودة جديدة"
  },

  ncrs: {
    title: "عنوان تقرير عدم المطابقة",
    titlePlaceholder: "مثال: عدم مطابقة أبعاد القطر الداخلي للشحنة",
    description: "وصف عدم المطابقة الفنية وشروط التجاوز",
    severity: "درجة الخطورة",
    createTitle: "فتح تقرير عدم مطابقة (NCR)",
    addContainmentTitle: "إضافة إجراء احتواء فوري للشحنة",
    containmentType: "نوع إجراء الاحتواء الفوري",
    quarantineStock: "حجز وعزل المخزون فورياً",
    haltProduction: "إيقاف خط الإنتاج مؤقتاً",
    customerNotification: "إشعار العملاء المتأثرين",
    supplierHold: "تعليق أوامر توريد المورد",
    actionDescription: "وصف الإجراء المتخذ فوراً لمنع التسرب",
    productVariantId: "معرف صنف المنتج",
    lotId: "رقم التشغيلة / الدفعة (Lot)",
    quantity: "الكمية المتأثرة بالإجراء"
  },

  capas: {
    title: "عنوان طلب الإجراء التصحيحي / الوقائي",
    titlePlaceholder: "مثال: معالجة الانحراف الحراري في أفران المعالجة",
    type: "نوع الإجراء",
    typeCorrective: "تصحيحي (Corrective — لمنع التكرار)",
    typePreventive: "وقائي (Preventive — لمنع الحدوث)",
    problemStatement: "صياغة وتوصيف المشكلة (5W2H)",
    createTitle: "فتح ملف إجراء تصحيحي/وقائي (CAPA)",
    addActionTitle: "إسناد وتكليف بإجراء تنفيذي",
    actionType: "نوع الإجراء التنفيذي",
    actionCorrective: "إجراء تصحيحي مباشر",
    actionPreventive: "إجراء وقائي جذري",
    actionProcess: "تعديل في إجراءات التشغيل المعيارية (SOP)",
    actionDescription: "تفاصيل الإجراء المطلوب تنفيذه",
    ownerId: "المسؤول عن التنفيذ",
    ownerPlaceholder: "اسم أو كود المهندس المسؤول",
    dueDate: "تاريخ الاستحقاق النهائي",
    completeActionTitle: "تأكيد إنجاز الإجراء الموكل",
    evidenceReference: "مرجع إثبات التنفيذ (تقرير، صورة، فحص)",
    evidencePlaceholder: "رقم التقرير أو رابط مستند التحقق",
    addRootCauseTitle: "توثيق تحليل الجذر المسبب (Root Cause)",
    rootCauseCategory: "تصنيف السبب الجذري (مخطط إيشيكاوا 6M)",
    categoryMan: "العنصر البشري والتدريب (Man)",
    categoryMachine: "الآلات والمعدات والمعايرة (Machine)",
    categoryMaterial: "المواد الخام والموردين (Material)",
    categoryMethod: "طريقة العمل والتعليمات (Method)",
    categoryMeasurement: "أجهزة القياس والدقة (Measurement)",
    categoryEnvironment: "بيئة العمل وظروف التشغيل (Environment)",
    rootCauseDescription: "الشرح التفصيلي للسبب الجذري بعد التحقيق",
    rootCauseVerified: "تم التحقق العملي من صحة هذا السبب",
    recordEffectivenessTitle: "تقييم واعتماد فعالية الإجراء (Effectiveness)",
    effectivenessResult: "نتيجة تقييم الفعالية بعد فترة التجربة",
    effectiveConfirmed: "فعال ومحقق للهدف (إغلاق نهائي)",
    ineffectiveRecurring: "غير فعال وتكررت المشكلة (إعادة فتح التحقيق)",
    evaluationNotes: "تقرير مراجعة الأدلة والبيانات الميدانية"
  },

  suppliers: {
    partyId: "معرف المورد (Party ID)",
    partyIdPlaceholder: "كود المورد في دليل الأطراف",
    riskLevel: "مستوى المخاطر المعتمد للمورد",
    targetPpm: "معدل العيوب المستهدف (PPM)",
    receivingInspectionRequired: "إلزامية الفحص المخبري عند كل استلام بضاعة",
    createProfile: "تسجيل سياسة جودة مورد جديد",
    editProfile: "تعديل معايير جودة المورد"
  },

  scars: {
    createTitle: "إصدار طلب تصحيحي للمورد (SCAR)",
    supplierPartyId: "كود المورد المعني",
    supplierPlaceholder: "كود المورد",
    title: "عنوان طلب الـ SCAR",
    titlePlaceholder: "مثال: تسليم شحنات بأسلاك معيبة بالأكسدة",
    description: "بيان العيوب الفنية والأضرار الناتجة ومطالب المعالجة",
    ncrId: "رقم تقرير عدم المطابقة المرتبط (اختياري)",
    dueDate: "الموعد النهائي لتلقي خطة المعالجة (8D)",
    submitResponseTitle: "تسجيل خطة رد المورد ومعالجة الـ SCAR",
    responseExplanation: "شرح خطة المورد للاحتواء والسبب الجذري وخطة المنع",
    evidenceReference: "مرجع تقرير المورد الفني (رقم مستند 8D)",
    reviewTitle: "مراجعة واعتماد رد المورد",
    reviewDecision: "القرار الفني بشأن رد المورد",
    decisionAccepted: "قبول الرد وإغلاق الطلب (Accepted)",
    decisionRejected: "رفض الرد وإعادة الطلب للمورد (Rejected)",
    reviewNotes: "ملاحظات وتوجيهات لجنة مراجعة جودة التوريد"
  },

  certificates: {
    issueTitle: "إصدار شهادة جودة وتحليل (COA/COC)",
    certificateType: "نوع الشهادة الرسمية",
    typeCoa: "شهادة تحليل واختبارات فنية (COA)",
    typeCoc: "شهادة مطابقة للمواصفات القياسية (COC)",
    customerReference: "المرجع الخاص بالعميل / أمر البيع",
    customerPlaceholder: "رقم أمر توريد العميل أو مرجعه",
    documentTitle: "وثيقة الجودة الرسمية المعتمدة",
    coaFullName: "شهادة فحص وتحليل مخبري معتمدة (COA)",
    cocFullName: "شهادة مطابقة معايير التصنيع والجودة (COC)",
    number: "الرقم المسلسل للشهادة",
    inspectionId: "رقم فحص الجودة المرجعي",
    issuedAt: "تاريخ وساعة الاعتماد",
    verificationStatus: "حالة التوثيق والسلامة الرقمية",
    tamperProofVerified: "موثقة رقمياً ضد التلاعب والتحريف",
    certificationStatement: "تشهد إدارة توكيد الجودة بأن العينات المفحوصة من هذه الدفعة قد اجتازت كافة الاختبارات الفيزيائية والكيميائية والمعايير القياسية بنجاح تام وفق متطلبات الجودة الصارمة."
  },

  messages: {
    loading: "جاري تحميل البيانات من الخادم...",
    noData: "لا توجد سجلات مطابقة لمعايير البحث الحالية.",
    notFound: "السجل المطلوب غير متوفر أو تم حذفه.",
    success: "تم تنفيذ العملية بنجاح تام.",
    confirmAction: "هل أنت متأكد من تنفيذ هذا الإجراء؟ لا يمكن التراجع عن العمليات المعتمدة.",
    passedNote: "تنبيه تشغيلي صارم: نجاح الفحص لا يعني الإفراج التلقائي عن المخزون؛ يتطلب الإفراج إجراءً صريحاً.",
    reconcileNote: "مطابقة الأثر: في حال كان الأثر المخزني معلقاً، يتم التحقق وإعادة المحاولة بنفس المفتاح لمنع التكرار."
  }
};

export const ar = qualityAr;
