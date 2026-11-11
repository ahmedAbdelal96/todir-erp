export const expensesAr = {
  title: "المصروفات ومستحقات الموظفين",
  subtitle: "إدارة نفقات العمل، سلف الموظفين، بطاقات الشركات، وتسوية المطالبات المالية",
  activeEntity: "الكيان القانوني النشط",
  badge: "نفقات ومستحقات الأعمال",
  defaultEntity: "شركة الأفق للتجارة العامة ذ.م.م",

  tabs: {
    selfService: "خدماتي ومصروفاتي",
    review: "مراجعة واعتماد المصروفات",
    advances: "سلف العمل",
    corporateCards: "بطاقات الشركات",
    reimbursements: "المستحقات والتسويات",
    reports: "التقارير وتحليل النفقات",
    settings: "إعدادات وسياسات المصروفات"
  },

  selfService: {
    title: "بوابة الموظف للنفقات والسلف",
    description: "تسجيل المصروفات اليومية، متابعة حالة الاعتماد، طلب سلف العمل، وتتبع مبالغ الاسترداد",
    newExpense: "تسجيل مصروف جديد",
    requestAdvance: "طلب سلفة عمل جديدة",
    subtabs: {
      myExpenses: "مصروفاتي",
      myAdvances: "سلف العمل الخاصة بي",
      myCards: "حركات بطاقة الشركة",
      myReimbursements: "مستحقاتي الواجبة الصرف",
      myRepayments: "المبالغ المستحقة عليّ للشركة",
      myReports: "تقارير المصروفات"
    },
    emptyExpenses: "لا توجد مصروفات مسجلة حتى الآن. انقر على 'تسجيل مصروف جديد' للبدء.",
    emptyAdvances: "لا توجد سلف عمل مسجلة حتى الآن.",
    emptyCards: "لا توجد حركات بطاقات شركات تتطلب اتخاذ إجراء.",
    emptyReimbursements: "لا توجد مستحقات سداد معلقة لك حالياً.",
    emptyRepayments: "لا توجد مطالبات سداد مستحقة عليك لصالح الشركة.",
    emptyReports: "لا توجد تقارير مصروفات مسجلة."
  },

  review: {
    title: "منظومة مراجعة واعتماد المصروفات",
    description: "فحص ومراجعة النفقات المقدمة، التحقق من الإيصالات وتطبيق السياسات المالية",
    searchPlaceholder: "البحث باسم التاجر أو الغرض من النفقة...",
    filterAll: "كافة الحالات",
    filterSubmitted: "بانتظار الاعتماد",
    filterApproved: "معتمدة",
    filterFinalized: "مرحلة بالدفاتر",
    filterReversed: "قيود عكسية",
    empty: "لا توجد مصروفات تطابق معايير البحث الحالية.",
    approveAction: "اعتماد",
    rejectAction: "رفض",
    finalizeAction: "ترحيل مالي",
    reverseAction: "قيد عكسي",
    reversalReasonPlaceholder: "سبب القيد العكسي...",
    reversalReasonLabel: "سبب القيد العكسي",
    confirmReverseAction: "تأكيد القيد العكسي",
    inspectAction: "معاينة التفاصيل",
    approvalCol: "الاعتماد",
    postingCol: "الترحيل",
    settlementCol: "التسوية"
  },

  advances: {
    title: "إدارة سلف العمل والتسويات",
    description: "متابعة السلف المصروفة للموظفين لتغطية نفقات السفر ومهمات العمل والتسوية مع المصروفات",
    requestAdvance: "طلب سلفة عمل",
    searchPlaceholder: "البحث في سلف العمل...",
    filterAll: "كافة السلف",
    filterRequested: "قيد المراجعة",
    filterApproved: "معتمدة بانتظار الصرف",
    filterIssued: "منصرفة وقائمة",
    filterSettled: "مسواة بالكامل",
    overdueWarning: "سلفة متأخرة عن موعد التسوية",
    empty: "لا توجد سلف عمل مسجلة حتى الآن.",
    issueAction: "صرف السلفة",
    applyAction: "تسوية بمصروف",
    returnAction: "رد نقدي",
    writeOffAction: "شطب السلفة",
    progressLabel: "نسبة تسوية السلفة:",
    disbursed: "المنصرف",
    applied: "المسوى بمصروف",
    returned: "المسترد",
    outstanding: "الرصيد القائم"
  },

  corporateCards: {
    title: "صندوق بطاقات الشركات والمطابقة",
    description: "استيراد ومطابقة حركات بطاقات الائتمان المؤسسية وتصنيف المصروفات الشخصية والعمليات المعدلة",
    importTransaction: "استيراد حركة بطاقة",
    reconciliationTitle: "ملخص المطابقة والتسوية البنكية",
    filterAll: "كافة الحركات",
    filterUnmatched: "غير مطابقة",
    filterMatched: "مطابقة بمصروف",
    filterPersonal: "مصروف شخصي",
    filterIgnored: "مستبعدة",
    empty: "لا توجد حركات بطاقات شركات مسجلة في هذا الكيان.",
    matchAction: "مطابقة بمصروف",
    createExpenseAction: "إنشاء مصروف من الحركة",
    personalAction: "تصنيف كمصروف شخصي",
    ignoreAction: "استبعاد",
    clearAction: "تسوية الحركة",
    adjustAction: "تسجيل تسوية بنكية",
    bankReference: "المرجع البنكي"
  },

  reimbursements: {
    title: "تسوية مستحقات الموظفين والمدفوعات",
    description: "إعداد دفعات السداد للموظفين عن المصروفات المعتمدة، ومتابعة سداد الموظفين للمصروفات الشخصية",
    prepareBatch: "تجهيز دفعة سداد جديدة",
    subtabs: {
      toEmployees: "مستحقات الموظفين (صرف)",
      fromEmployees: "مستحقات الشركة طرف الموظفين (تحصيل)"
    },
    batchesTitle: "دفعات السداد المجهزة",
    payBatchAction: "تنفيذ الصرف",
    settleRepaymentAction: "تسجيل التحصيل",
    emptyBatches: "لا توجد دفعات سداد معلقة حالياً.",
    emptyRepayments: "لا توجد مطالبات مستحقة على الموظفين.",
    batchId: "معرف الدفعة",
    preparedDate: "تاريخ الإعداد",
    batchTotal: "إجمالي الدفعة",
    batchPreparedStatus: "جاهزة للصرف",
    batchPaidStatus: "تم الصرف",
    reference: "المرجع",
    entryDate: "تاريخ القيد",
    totalAmount: "المبلغ الإجمالي",
    collectedAmount: "المحصل",
    remainingAmount: "المتبقي",
    collectedStatus: "تم التحصيل",
    dueStatus: "مستحق التحصيل",
    claimDate: "تاريخ المطالبة",
    dueAmount: "المبلغ المستحق",
    paidAmount: "المبلغ المصروف"
  },

  reports: {
    title: "تقارير وتحليلات نفقات الأعمال",
    description: "تحليل إجمالي المنصرفات حسب التصنيف، الموظف، ومصدر الدفع مع إمكانية التصدير المحاسبي",
    exportCsv: "تصدير تقرير النفقات (CSV)",
    groupByCategory: "تجميع حسب التصنيف",
    groupByEmployee: "تجميع حسب الموظف",
    groupBySource: "تجميع حسب مصدر الدفع",
    fromDate: "من تاريخ",
    toDate: "إلى تاريخ",
    empty: "لا توجد بيانات مصروفات للفترة المحددة.",
    totalSpend: "إجمالي المنصرفات",
    totalBase: "الإجمالي بالعملة الأساسية",
    totalTax: "إجمالي الضرائب القابلة للاسترداد",
    groupByLabel: "معيار التجميع والتحليل",
    groupHeader: "المجموعة / البند",
    expenseCount: "عدد النفقات",
    unclassified: "غير مصنف",
    reportTitle: "عنوان التقرير",
    createdAt: "تاريخ الإنشاء"
  },

  settingsTab: {
    title: "إعدادات وسياسات المصروفات",
    description: "تهيئة تصنيفات النفقات، حدود الصرف، سياسات التدقيق، وبدلات السفر والانتقال",
    newCategory: "إضافة تصنيف جديد",
    newPolicy: "إضافة سياسة مصروفات",
    newRatePolicy: "إضافة بدل انتقال أو سفر",
    importExpenses: "استيراد مجمع للمصروفات",
    subtabs: {
      categories: "تصنيفات المصروفات",
      policies: "سياسات وتنبيهات الصرف",
      ratePolicies: "بدلات المسافات والسفر",
      imports: "دفعات الاستيراد"
    },
    emptyCategories: "لا توجد تصنيفات معرفة.",
    emptyPolicies: "لا توجد سياسات مصروفات معرفة.",
    emptyRatePolicies: "لا توجد بدلات مسافات أو سفر معرفة.",
    emptyImports: "لا توجد دفعات استيراد سابقة.",
    categoryCode: "كود التصنيف",
    categoryName: "اسم التصنيف",
    publishedVersion: "الإصدار المنشور",
    publishedActive: "منشور ومفعل",
    draftStatus: "مسودة",
    newVersionAction: "+ إصدار جديد",
    policyCode: "كود السياسة",
    policyName: "اسم السياسة",
    newRulesAction: "+ قواعد جديدة",
    rateKind: "النوع",
    rateValue: "المعدل",
    effectiveFrom: "ساري من",
    publishAction: "نشر وتفعيل"
  },

  kpi: {
    pendingApproval: "بانتظار الاعتماد",
    missingEvidence: "إيصالات مفقودة",
    reimbursementPending: "مستحقات واجبة السداد",
    personalRepaymentDue: "مبالغ مستحقة على موظفين",
    outstandingAdvances: "سلف عمل قائمة",
    statementAmount: "إجمالي كشف البطاقات",
    matchedAmount: "حركات مطابقة",
    personalAmount: "مصروفات شخصية",
    unclassifiedAmount: "حركات غير مصنفة"
  },

  states: {
    expense: {
      Draft: "مسودة",
      Submitted: "مقدم للاعتماد",
      Approved: "معتمد",
      Finalized: "مرحل مالياً",
      Reversed: "قيد عكسي"
    },
    approval: {
      Pending: "قيد المراجعة",
      Approved: "معتمد",
      Rejected: "مرفوض"
    },
    accounting: {
      NotFinalized: "غير مرحل",
      Pending: "قيد الترحيل",
      Posted: "مرحل بالدفاتر",
      Reversed: "تم العكس"
    },
    settlement: {
      NotSettled: "غير مسوى",
      ReimbursementPending: "بانتظار الصرف للموظف",
      ReimbursementPaid: "تم الصرف للموظف",
      RepaymentPending: "بانتظار سداد الموظف",
      RepaymentSettled: "تم سداد الموظف",
      AdvanceApplied: "تمت التسوية من السلفة",
      SettledDirect: "مسوى مباشرة"
    },
    evidence: {
      Missing: "مفقود",
      PendingVerification: "قيد الفحص",
      Verified: "موثق وسليم",
      Rejected: "مرفوض"
    },
    policy: {
      NotEvaluated: "لم يفحص",
      Passed: "مطابق للسياسة",
      Warning: "تنبيه تجاوز",
      Violation: "مخالفة سياسة",
      ExceptionApproved: "استثناء معتمد"
    },
    source: {
      EmployeePaid: "مدفوع من الموظف شخصياً",
      CompanyCard: "بطاقة الشركة",
      PettyCash: "عهدة نقدية",
      Advance: "خصماً من سلفة عمل"
    },
    advance: {
      Requested: "مطلوبة",
      Approved: "معتمدة",
      Issued: "منصرفة",
      Applied: "مطبقة جزئياً",
      PartiallySettled: "مسواة جزئياً",
      Settled: "مسواة بالكامل"
    },
    card: {
      Unmatched: "غير مطابق",
      Matched: "مطابق بمصروف",
      Personal: "مصروف شخصي",
      Ignored: "مستبعد"
    }
  },

  common: {
    merchant: "التاجر / الجهة",
    date: "التاريخ",
    amount: "المبلغ",
    currency: "العملة",
    source: "مصدر الدفع",
    category: "التصنيف",
    purpose: "الغرض من النفقة",
    description: "الوصف والملاحظات",
    status: "الحالة",
    actions: "الإجراءات",
    save: "حفظ",
    cancel: "إلغاء",
    confirm: "تأكيد",
    submit: "إرسال للاعتماد",
    close: "إغلاق",
    loading: "جاري التحميل...",
    errorTitle: "حدث خطأ غير متوقع",
    retry: "إعادة المحاولة",
    reason: "سبب الإجراء (إلزامي)",
    notAvailable: "غير محدد",
    yes: "نعم",
    no: "لا",
    file: "الملف المرفق",
    uploadReceipt: "إرفاق إيصال",
    declareMissingReceipt: "إقرار فقدان الإيصال",
    details: "تفاصيل",
    history: "سجل العمليات",
    participants: "المشاركون",
    itemizations: "بنود التفكيك",
    mileage: "بدل المسافة",
    perDiem: "بدل الإقامة والسفر",
    dimensions: "الأبعاد ومراكز التكلفة",
    noLegalEntitySelected: "لم يتم اختيار كيان قانوني",
    selectLegalEntityNotice: "يرجى تحديد الكيان القانوني المصرح به من شريط التنقل العلوي لبدء إدارة المصروفات والمستحقات."
  },

  modals: {
    newExpense: {
      title: "تسجيل مصروف جديد",
      description: "أدخل بيانات النفقة وتفاصيل التاجر والقيمة ومصدر السداد",
      merchantLabel: "اسم التاجر أو مزود الخدمة",
      dateLabel: "تاريخ النفقة",
      amountLabel: "المبلغ المالي",
      currencyLabel: "العملة",
      sourceLabel: "مصدر الدفع",
      categoryLabel: "تصنيف المصروف",
      purposeLabel: "الغرض من المصروف",
      descriptionLabel: "ملاحظات إضافية",
      submitButton: "تسجيل المصروف"
    },
    declareMissingReceipt: {
      title: "إقرار فقدان الإيصال الرسمي",
      description: "وفقاً للسياسات المالية للشركة، يتطلب عدم إرفاق إيصال إقراراً رسمياً وتوضيحاً للمبررات",
      reasonLabel: "سبب تعذر تقديم الإيصال أو المستند المؤيد",
      reasonPlaceholder: "مثال: لم يوفر التاجر فاتورة ورقية / فقدت الفاتورة أثناء السفر...",
      submitButton: "تقديم الإقرار الرسمي"
    },
    requestAdvance: {
      title: "طلب سلفة عمل جديدة",
      description: "طلب صرف مبلغ مالي مقدماً لتغطية مصاريف مهام أو سفريات العمل",
      purposeLabel: "الغرض من السلفة والمهمة",
      amountLabel: "مبلغ السلفة المطلوب",
      currencyLabel: "العملة",
      dueDateLabel: "تاريخ استحقاق التسوية المتوقع",
      submitButton: "إرسال طلب السلفة"
    },
    issueAdvance: {
      title: "صرف سلفة العمل للموظف",
      description: "تأكيد صرف المبلغ المالي عبر حساب الدفع المحدد في النظام",
      accountLabel: "حساب الدفع / الخزينة",
      methodLabel: "طريقة الصرف",
      amountLabel: "المبلغ المنصرف",
      paymentDateLabel: "تاريخ الصرف",
      postingDateLabel: "تاريخ القيد المحاسبي",
      submitButton: "تأكيد صرف السلفة",
      methods: {
        bankTransfer: "تحويل بنكي (Bank Transfer)",
        cash: "صرف نقدي من الخزينة (Cash)",
        cheque: "شيك مصرفي (Cheque)"
      }
    },
    applyAdvance: {
      title: "تسوية مصروف خصماً من السلفة",
      description: "تطبيق مصروف معتمد لخصمه من رصيد سلفة العمل القائمة",
      expenseLabel: "المصروف المطلوب تسويته",
      amountLabel: "المبلغ المخصوم من السلفة",
      submitButton: "تطبيق التسوية"
    },
    returnAdvance: {
      title: "رد متبقي سلفة العمل للشركة",
      description: "تسجيل استلام المبلغ المتبقي غير المنفق من سلفة العمل",
      amountLabel: "المبلغ المسترد للشركة",
      submitButton: "تأكيد رد المبلغ"
    },
    writeOffAdvance: {
      title: "طلب شطب سلفة عمل",
      description: "طلب شطب الرصيد المتبقي أو المتعثر للسلفة بموافقة الإدارة المالية",
      amountLabel: "مبلغ الشطب",
      reasonLabel: "سبب ومبرر الشطب المحاسبي",
      submitButton: "تقديم طلب الشطب"
    },
    prepareBatch: {
      title: "تجهيز دفعة سداد مستحقات موظفين",
      description: "تجميع كافة المصروفات المعتمدة المرحلة بالدفاتر في دفعة سداد واحدة",
      currencyLabel: "عملة الدفعة",
      submitButton: "تجهيز الدفعة"
    },
    payBatch: {
      title: "صرف دفعة سداد المستحقات",
      description: "تنفيذ الصرف البنكي أو النقدي لدفعة مستحقات الموظفين",
      accountLabel: "حساب الصرف",
      methodLabel: "طريقة الدفع",
      paymentDateLabel: "تاريخ الدفع",
      postingDateLabel: "تاريخ القيد",
      submitButton: "تأكيد الصرف المالي"
    },
    settleRepayment: {
      title: "تحصيل مبلغ مستحق من موظف",
      description: "تسجيل تحصيل المبالغ المستحقة للشركة نظير مصروفات بطاقة شخصية",
      accountLabel: "حساب الاستلام",
      methodLabel: "طريقة التحصيل",
      amountLabel: "المبلغ المحصل",
      paymentDateLabel: "تاريخ الاستلام",
      postingDateLabel: "تاريخ القيد",
      submitButton: "تأكيد التحصيل"
    },
    matchCard: {
      title: "مطابقة حركة البطاقة مع مصروف",
      description: "ربط حركة بطاقة الائتمان بمصروف معتمد مسجل في النظام",
      expenseIdLabel: "معرف المصروف المسجل",
      submitButton: "تأكيد المطابقة"
    },
    createExpenseFromCard: {
      title: "إنشاء مصروف من حركة البطاقة",
      description: "استخدام بيانات التاجر والقيمة لتحويل حركة البطاقة إلى مصروف عمل معتمد",
      categoryLabel: "تصنيف المصروف",
      purposeLabel: "الغرض من النفقة",
      descriptionLabel: "ملاحظات إضافية",
      submitButton: "إنشاء المصروف"
    },
    recordAdjustment: {
      title: "تسجيل تسوية لحركة البطاقة",
      description: "تسجيل مردودات، تسويات، أو فروق تحويل لحركة البطاقة",
      kindLabel: "نوع التسوية (مردود / نزاع / عكس)",
      referenceLabel: "المرجع البنكي للتسوية",
      amountLabel: "المبلغ",
      currencyLabel: "العملة",
      reasonLabel: "سبب التسوية",
      submitButton: "تسجيل التسوية"
    },
    newCategory: {
      title: "إضافة تصنيف مصروفات جديد",
      description: "تحديد اسم ورمز التصنيف ومتطلبات إرفاق الإيصال",
      codeLabel: "رمز التصنيف (Code)",
      nameLabel: "اسم التصنيف",
      requiresReceiptLabel: "يتطلب تقديم إيصال إلزامي",
      submitButton: "إنشاء التصنيف"
    },
    newCategoryVersion: {
      title: "نشر إصدار جديد لتصنيف المصروفات",
      description: "تحديث حد الإيصال وتواريخ السريان للتصنيف",
      requiresReceiptLabel: "يتطلب إيصال رسمي",
      thresholdLabel: "حد الإعفاء من الإيصال (اختياري)",
      effectiveFromLabel: "ساري من تاريخ",
      effectiveToLabel: "ساري حتى تاريخ (اختياري)",
      submitButton: "حفظ الإصدار"
    },
    newPolicy: {
      title: "إضافة سياسة مصروفات جديدة",
      description: "تعريف سياسة رقابية وقواعد تدقيق لنفقات العمل",
      codeLabel: "رمز السياسة (Code)",
      nameLabel: "اسم السياسة",
      submitButton: "إنشاء السياسة"
    },
    newPolicyVersion: {
      title: "إصدار قواعد سياسة المصروفات",
      description: "تحديد القواعد وتاريخ السريان للسياسة الرقابية",
      rulesLabel: "قواعد السياسة (صيغة JSON)",
      effectiveFromLabel: "ساري من تاريخ",
      effectiveToLabel: "ساري حتى تاريخ",
      submitButton: "نشر القواعد",
      invalidJson: "صيغة JSON غير صحيحة لقواعد السياسة"
    },
    newRatePolicy: {
      title: "إضافة سياسة بدلات انتقال وسفر",
      description: "تحديد معدلات بدلات المسافات (الكيلومترات) وبدلات الإقامة اليومية",
      kindLabel: "نوع البدل",
      kindMileage: "بدل مسافات وانتقال (Mileage)",
      kindPerDiem: "بدل إقامة يومي (Per Diem)",
      rateLabel: "المعدل / السعر",
      currencyLabel: "العملة",
      effectiveFromLabel: "ساري من تاريخ",
      effectiveToLabel: "ساري حتى تاريخ",
      vehicleTypeLabel: "نوع المركبة (لبدل المسافات)",
      locationCodeLabel: "رمز الوجهة أو المدينة (لبدل السفر)",
      breakfastReductionLabel: "خصم وجبة الإفطار",
      lunchReductionLabel: "خصم وجبة الغداء",
      dinnerReductionLabel: "خصم وجبة العشاء",
      submitButton: "حفظ البدل"
    },
    importExpenses: {
      title: "استيراد مجمع للمصروفات",
      description: "رفع وتضمين سجلات مصروفات مجمعة دفعة واحدة للنظام",
      fileLabel: "بيانات السجلات (JSON مصفوفة أسطر)",
      submitButton: "بدء الاستيراد",
      invalidArray: "يجب أن تكون المدخلات مصفوفة JSON صالحة",
      invalidJson: "صيغة JSON غير صحيحة"
    }
  },

  drawer: {
    title: "تفاصيل وملف المصروف 360",
    subtitle: "المعلومات المالية، الإيصالات، مراجعة السياسات، وسجل الاعتمادات",
    overview: "نظرة عامة",
    evidenceTab: "الإيصالات والمستندات",
    policyTab: "فحص السياسات والاستثناءات",
    itemizationTab: "تفكيك البنود",
    participantsTab: "المشاركون",
    travelTab: "الانتقال وبدل السفر",
    dimensionsTab: "الأبعاد المحاسبية",
    historyTab: "سجل التغييرات",
    noEvidence: "لم يتم إرفاق إيصالات لهذا المصروف.",
    addEvidence: "إرفاق مستند جديد",
    missingReceiptDeclared: "تم تسجيل إقرار رسمي بفقدان الإيصال لهذا المصروف بمبرر موثق.",
    noItemizations: "لا توجد بنود تفكيك مسجلة.",
    addItemization: "إضافة بند تفكيك",
    noParticipants: "لا يوجد مشاركون مسجلون.",
    addParticipant: "إضافة مشارك",
    noDimensions: "لا توجد أبعاد محاسبية مخصصة.",
    setDimension: "تحديد بعد محاسبي",
    requestException: "طلب استثناء من السياسة",
    approveException: "اعتماد الاستثناء",
    rejectException: "رفض الاستثناء",
    financialOutcome: "الأثر المالي والقيد المحاسبي",
    baseAmount: "القيمة بالعملة الأساسية",
    taxAmount: "مبلغ الضريبة القابل للاسترداد",
    exceptionReasonPlaceholder: "توضيح مبرر تجاوز الحد المالي للسياسة...",
    itemDescriptionPlaceholder: "وصف البند",
    participantRole: "الصفة",
    participantNamePlaceholder: "اسم المشارك",
    dimensionCode: "كود البعد",
    dimensionValue: "القيمة"
  },

  evidenceUpload: {
    uploadTitle: "إرفاق مستند مؤيد رسمي",
    fileLabel: "اختيار ملف الإيصال أو الفاتورة",
    fileHint: "الملفات المدعومة: PDF, PNG, JPG حتى 15 ميجابايت",
    typeLabel: "نوع المستند المؤيد",
    submitButton: "رفع وفحص المستند",
    uploading: "جاري الرفع والفحص الأمني...",
    uploadSuccess: "تم رفع المستند وفحصه أمنياً بنجاح",
    uploadError: "فشل رفع المستند المؤيد",
    downloadAction: "تحميل المستند",
    startOcrAction: "قراءة آلية (OCR)",
    ocrJobPending: "جاري المعالجة الآلية...",
    ocrSuccessTitle: "بيانات الفاتورة المستخرجة آلياً",
    applyOcrValues: "تطبيق القيم على المصروف"
  },

  historyActions: {
    Created: "تم إنشاء السجل كمسودة",
    Submitted: "تم إرسال المصروف للاعتماد",
    Approved: "تم اعتماد المصروف من الإدارة",
    Rejected: "تم رفض المصروف",
    Finalized: "تم ترحيل القيد إلى الأستاذ العام",
    Reversed: "تم إجراء قيد عكسي للمصروف",
    EvidenceAdded: "تم إرفاق مستند مؤيد جديد",
    MissingReceiptDeclared: "تم تقديم إقرار فقدان إيصال",
    PolicyEvaluated: "تم فحص الامتثال للسياسات المالية",
    ItemizationAdded: "تم تفكيك بند من المصروف",
    ParticipantAdded: "تم تسجيل مشارك في النفقة",
    DimensionSet: "تم تحديد بعد محاسبي",
    Updated: "تم تحديث بيانات المصروف"
  },

  participantRoles: {
    MealParticipant: "مشارك في وجبة / ضيافة عمل",
    ProjectMember: "عضو في فريق المشروع",
    TravelCompanion: "مرافق في مهمة عمل",
    ExternalGuest: "عميل أو ضيف خارجي"
  },

  evidenceTypes: {
    TaxInvoice: "فاتورة ضريبية رسمية",
    Receipt: "إيصال دفع نقدي أو إلكتروني",
    Ticket: "تذكرة سفر أو انتقال",
    Contract: "عقد أو إذن تكليف",
    Other: "مستند مؤيد آخر"
  },

  mileagePerDiem: {
    modeGeneral: "مصروف عام",
    modeMileage: "بدل انتقال (كيلومتر)",
    modePerDiem: "بدل سفر يومي (إعاشة)",
    distanceKm: "المسافة المقطوعة (كم)",
    roundTrip: "رحلة ذهاب وعودة",
    ratePerKm: "سعر الكيلومتر",
    daysCount: "عدد الأيام المستحقة",
    dailyRate: "البدل اليومي المعتمد",
    calculatedNotice: "المبلغ محسوب آلياً وفق سياسة البدلات المعتمدة:",
    routePlaceholder: "مثال: من المقر الرئيسي إلى فرع الإسكندرية",
    locationPlaceholder: "مثال: دبي / الرياض / أسوان",
    policyLabel: "السياسة المعتمدة",
    noMileagePolicyNotice: "لا توجد سياسة معتمدة لبدل الانتقال سارية في هذا التاريخ لهذه المنشأة",
    noPerDiemPolicyNotice: "لا توجد سياسة معتمدة لبدل السفر والإقامة سارية في هذا التاريخ لهذه المنشأة",
    vehicleType: "نوع المركبة",
    providedMeals: "الوجبات الموفرة",
    providedMealsNone: "لا توجد وجبات موفرة (استحقاق كامل)",
    providedMealsOne: "وجبة واحدة موفرة (خصم ثلث البدل)",
    providedMealsTwo: "وجبتان موفرتان (خصم ثلثي البدل)",
    providedMealsThree: "3 وجبات موفرة (إقامة شاملة)"
  }
} as const;
