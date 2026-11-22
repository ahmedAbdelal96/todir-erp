export const ar = {
  title: "الموارد البشرية",
  subtitle: "إدارة شؤون الموظفين والهيكل التنظيمي والحضور والإجازات والملف الرقمي",
  helpButton: "دليل الاستخدام والتعليمات",
  helpTitle: "دليل إدارة الموارد البشرية",
  helpDescription: "يوفر هذا الموديول إدارة متكاملة للموظفين والعقود والتنظيم والحضور والانصراف مع مراعاة الخصوصية والأثر المحاسبي.",

  tabs: {
    home: "الرئيسية",
    directory: "سجل الموظفين",
    attendance: "الحضور والانصراف",
    leave: "الإجازات والأرصدة",
    organization: "الهيكل التنظيمي",
    selfservice: "الخدمة الذاتية",
    manager: "إدارة فريقي",
    reports: "التقارير",
    imports: "الترحيل والاستيراد"
  },

  actions: {
    cancel: "إلغاء",
    confirm: "تأكيد",
    save: "حفظ",
    saving: "جارٍ الحفظ...",
    loading: "جارٍ التحميل...",
    close: "إغلاق",
    retry: "إعادة المحاولة",
    action: "الإجراء",
    verify: "تحقق",
    submit: "إرسال",
    submitting: "جارٍ الإرسال...",
    upload: "رفع",
    uploading: "جارٍ الرفع..."
  },

  employeeStatus: {
    Active: "نشط",
    Inactive: "غير نشط",
    Archived: "مؤرشف"
  },

  employmentStatus: {
    PreHire: "قيد التعيين",
    Active: "على رأس العمل",
    Suspended: "موقوف مؤقتاً",
    NoticePeriod: "فترة إشعار",
    Terminated: "منتهية خدماته",
    Ended: "منتهي"
  },

  employmentType: {
    FullTime: "دوام كامل",
    PartTime: "دوام جزئي",
    Temporary: "مؤقت",
    FixedTerm: "محدد المدة",
    Probationary: "فترة تجربة",
    Intern: "متدرب",
    Seasonal: "موسمي",
    Other: "أخرى"
  },

  statuses: {
    Active: "نشط",
    Inactive: "غير نشط",
    Pending: "قيد المراجعة",
    Approved: "معتمد",
    Rejected: "مرفوض",
    Cancelled: "ملغى",
    Verified: "معتمد وموثق",
    Draft: "مسودة",
    Present: "حاضر",
    Absent: "غائب",
    Exception: "حالة شاذة",
    Late: "متأخر",
    Open: "مفتوح",
    Locked: "مقفل للرواتب",
    PreHire: "قيد التعيين",
    Probationary: "فترة تجربة",
    NoticePeriod: "فترة إشعار",
    Terminated: "منتهية خدماته",
    Ended: "منتهي",
    Suspended: "موقوف مؤقتاً",
    Archived: "مؤرشف"
  },

  frequencies: {
    Monthly: "شهري",
    Weekly: "أسبوعي",
    BiWeekly: "كل أسبوعين",
    Hourly: "بالساعة"
  },

  home: {
    title: "مركز عمليات الموارد البشرية",
    subtitle: "متابعة المهام اليومية، التنبيهات الحرجة، وتراخيص العمل والعقود",
    actions: {
      newEmployee: "إضافة موظف جديد",
      viewAttendance: "حركات الحضور اليومية"
    },
    kpi: {
      activeEmployees: "الموظفون النشطون",
      activeEmployeesDesc: "إجمالي الموظفين على رأس العمل حالياً",
      pendingLeave: "طلبات إجازة معلقة",
      pendingLeaveDesc: "تتطلب موافقة المدير أو الموارد البشرية",
      missingAttendance: "حالات حضور شاذة",
      missingAttendanceDesc: "بصمات دخول دون انصراف مسجل",
      expiringDocs: "مستندات قاربت الانتهاء",
      expiringDocsDesc: "وثائق تنتهي خلال 30 يوماً"
    },
    compliance: {
      title: "متابعة الالتزامات القادمة",
      description: "ملخص مختصر للمستندات والعقود التي تحتاج مراجعة خلال 30 يوماً.",
      expiringDocuments: "مستندات تحتاج متابعة",
      expiringContracts: "عقود تحتاج متابعة",
      expiringContractsDesc: "عقود تقترب من تاريخ الانتهاء",
      noData: "لا توجد بيانات التزامات متاحة حالياً."
    },
    states: {
      loading: "جارٍ تحميل ملخص الموارد البشرية...",
      error: "تعذر تحميل ملخص الموارد البشرية. حاول مرة أخرى."
    },
    reminders: {
      title: "التنبيهات التشغيلية والالتزامات النظامية",
      empty: "لا توجد تنبيهات عاجلة حالياً.",
      category: "التصنيف",
      task: "المهمة / المستند",
      employee: "الموظف",
      dueDate: "تاريخ الاستحقاق",
      severity: "الأهمية",
      categories: {
        DocumentExpiry: "انتهاء تصريح / وثيقة",
        MissingCheckout: "بصمة خروج مفقودة",
        ProbationReview: "مراجعة فترة التجربة",
        ContractRenewal: "تجديد عقد عمل"
      },
      severities: {
        danger: "حرج",
        warning: "تنبيه",
        info: "معلومة"
      }
    }
  },

  directory: {
    searchPlaceholder: "البحث بالاسم، الرقم الوظيفي، أو المسمى...",
    newEmployeeAction: "إضافة موظف جديد",
    tableAria: "جدول دليل الموظفين",
    emptyState: "لم يتم العثور على موظفين مطابقين لخيارات البحث.",
    loading: "جارٍ التحميل...",
    viewProfile: "الملف الشامل",
    totalRecords: "إجمالي السجلات: {{count}}",
    columns: {
      number: "الرقم الوظيفي",
      name: "اسم الموظف",
      englishName: "الاسم بالإنجليزية",
      startDate: "تاريخ التعيين",
      status: "الحالة",
      actions: "الإجراءات"
    },
    newEmployeeModal: {
      title: "إضافة موظف جديد إلى المنشأة",
      arabicName: "الاسم باللغة العربية",
      arabicNamePlaceholder: "الاسم الرباعي المعتمد...",
      englishName: "الاسم باللغة الإنجليزية",
      englishNamePlaceholder: "Full Name in English...",
      preferredName: "الاسم المفضل / الشهرة",
      preferredNamePlaceholder: "اسم الشهرة في مكان العمل...",
      employmentType: "نوع التوظيف",
      startDate: "تاريخ بدء العمل",
      endDateOptional: "تاريخ نهاية العقد (اختياري)",
      submit: "تأكيد وإنشاء الموظف"
    }
  },

  workspace: {
    employeeNotFound: "لم يتم العثور على بيانات الموظف المحدد.",
    backToDirectory: "العودة لسجل الموظفين",
    header: {
      englishName: "الاسم بالإنجليزية",
      employmentNumber: "الرقم الوظيفي",
      startDate: "تاريخ المباشرة",
      employmentStatus: "حالة التوظيف"
    },
    actions: {
      editProfile: "تعديل البيانات الأساسية",
      promoteTransfer: "ترقية / نقل",
      newContract: "عقد جديد",
      uploadDoc: "رفع مستند",
      terminate: "إنهاء خدمة",
      rehire: "إعادة تعيين"
    },
    profileEdit: {
      title: "تعديل البيانات الأساسية للموظف",
      arabicName: "الاسم باللغة العربية",
      englishName: "الاسم باللغة الإنجليزية",
      preferredName: "الاسم المفضل / الشهرة",
      reason: "سبب التعديل",
      submit: "حفظ التعديل"
    },
    tabs: {
      overview: "نظرة عامة",
      assignments: "التعيينات والترقيات",
      contracts: "العقود والملحقات",
      compensation: "الأجور والمزايا",
      documents: "الملف الرقمي والوثائق",
      qualifications: "المؤهلات والرخص",
      insurance: "التأمين والامتثال",
      timeline: "سجل الأحداث والتاريخ"
    },
    overview: {
      currentAssignmentTitle: "التعيين الوظيفي الحالي",
      documentCompletenessTitle: "اكتمال الملف والوثائق الإلزامية",
      verifiedCount: "المستندات المعتمدة",
      isComplete: "اكتمال الملف",
      complete: "مكتمل",
      incomplete: "غير مكتمل",
      noCurrentAssignment: "لا يوجد تعيين إداري نشط حالياً."
    },
    assignment: {
      historyTitle: "سجل التعيينات الإدارية والترقيات",
      promotionModalTitle: "ترقية موظف واعتماد مسمى جديد",
      transferModalTitle: "نقل إداري بين الإدارات أو الفروع",
      effectiveFrom: "يسري من تاريخ",
      effectiveTo: "حتى تاريخ",
      effectiveToOptional: "حتى تاريخ (اختياري)",
      reasonCode: "سبب الحركة",
      department: "القسم / الإدارة",
      position: "المسمى الوظيفي",
      manager: "المدير المباشر",
      version: "الإصدار",
      current: "حالي ومستمر",
      emptyHistory: "لا توجد تعيينات تاريخية سابقة.",
      reasons: {
        Promotion: "ترقية دورية أو استثنائية",
        Transfer: "نقل بين الفروع أو الإدارات",
        Restructure: "إعادة هيكلة تنظيمية",
        InitialHire: "تعيين أولي",
        InitialHiring: "تعيين أولي"
      }
    },
    contracts: {
      title: "عقود العمل وملحقات التجديد",
      newModalTitle: "تحرير عقد عمل جديد",
      renewModalTitle: "تجديد عقد عمل وإضافة ملحق",
      type: "نوع العقد",
      startDate: "تاريخ البداية",
      endDate: "تاريخ النهاية",
      probationEndOptional: "نهاية فترة التجربة (اختياري)",
      workingHoursBasis: "نظام الدوام",
      workingHoursBasisDescription: "يحدد طريقة احتساب ساعات العمل لهذا العقد. اتركه فارغًا لاستخدام الساعات القياسية المعتمدة.",
      workingHoursBasisPlaceholder: "مثال: دوام كامل بالساعات القياسية",
      autoActivateLabel: "تفعيل العقد مباشرة في سجل الموظف",
      status: "حالة العقد",
      actions: "الإجراءات",
      indefinite: "غير محدد المدة",
      empty: "لا توجد عقود عمل محررة للموظف.",
      renewAction: "تجديد / تمديد",
      renewalEffectiveDate: "تاريخ سريان التجديد",
      renewalTermsSummary: "بيان التجديد والشروط الإضافية",
      renewalTermsPlaceholder: "مثال: تجديد العقد لمدة سنة إضافية بنفس المزايا...",
      renewalTermsRequired: "يرجى توضيح ملخص شروط التجديد أو التمديد.",
      renewConfirm: "تأكيد تجديد العقد",
      types: {
        Definite: "محدد المدة",
        Indefinite: "غير محدد المدة",
        ProjectBased: "مرتبط بمشروع",
        Seasonal: "موسمي"
      }
    },
    compensation: {
      title: "سجل الأجور والتعويضات التعاقدية",
      amount: "المبلغ الإجمالي",
      currency: "العملة",
      frequency: "دورية الصرف",
      effectiveFrom: "تاريخ السريان",
      status: "الحالة",
      empty: "لا توجد بيانات أجور معلنة أو مصرح بالاطلاع عليها."
    },
    documents: {
      title: "المستندات والوثائق الرسمية",
      uploadModalTitle: "رفع وثيقة جديدة للملف الرقمي",
      category: "نوع المستند",
      titleLabel: "عنوان الوثيقة",
      titlePlaceholder: "مثال: صورة الهوية الوطنية، بطاقة الإقامة...",
      titleRequired: "يرجى تحديد عنوان واضح للوثيقة المرفوعة.",
      expiryDate: "تاريخ الانتهاء",
      expiryDateOptional: "تاريخ الانتهاء (اختياري)",
      fileName: "اسم الملف",
      isSensitiveLabel: "وثيقة حساسة للغاية (تقييد الوصول)",
      verified: "حالة الاعتماد",
      verify: "تحقق",
      download: "عرض / تنزيل",
      empty: "لا توجد مستندات مرفوعة للموظف حالياً.",
      fileRequired: "يرجى اختيار ملف المستند",
      fileTooLarge: "حجم الملف يتجاوز الحد المسموح به (15 ميجابايت)",
      invalidFileType: "نوع الملف غير مدعوم. الأنواع المدعومة: PDF والصور",
      categories: {
        NationalId: "بطاقة الهوية الوطنية / الإقامة",
        Passport: "جواز السفر",
        WorkPermit: "رخصة العمل",
        Contract: "عقد العمل الموثق",
        Certificate: "شهادة دراسية / تخرج",
        EducationalCertificate: "شهادة دراسية / تخرج",
        Medical: "تقرير الكشف الطبي",
        MedicalFitness: "تقرير الكشف الطبي",
        Other: "وثيقة إدارية أخرى"
      }
    },
    qualifications: {
      title: "المؤهلات العلمية والشهادات المهنية",
      level: "المستوى العلمي",
      institution: "الجهة المانحة / الجامعة",
      year: "سنة التخرج",
      empty: "لا توجد مؤهلات مسجلة."
    },
    insurance: {
      title: "التأمين الطبي والاجتماعي",
      scheme: "النظام التأميني",
      memberNumber: "رقم الاشتراك",
      effectiveFrom: "تاريخ بدء التغطية",
      status: "الحالة",
      empty: "لا توجد اشتراكات تأمينية مسجلة."
    },
    timeline: {
      title: "السجل التاريخي وتطورات الموظف",
      empty: "لا توجد أحداث تاريخية مسجلة بعد."
    },
    termination: {
      modalTitle: "إنهاء خدمة الموظف",
      disclaimer: "سيؤدي هذا الإجراء إلى جدولة إنهاء خدمات الموظف وإيقاف التعيين النشط مع الحفاظ التام على السجل التاريخي.",
      reason: "سبب إنهاء الخدمة",
      effectiveDate: "تاريخ آخر يوم عمل",
      reasons: {
        Resignation: "استقالة برغبة الموظف",
        MutualAgreement: "إنهاء بالتراضي بين الطرفين",
        ContractExpiration: "انتهاء مدة العقد دون تجديد",
        Dismissal: "فصل بموجب لوائح العمل",
        Retirement: "بلوغ سن التقاعد النظامي"
      }
    },
    rehire: {
      modalTitle: "إعادة تعيين موظف سابق",
      disclaimer: "إعادة التعيين تُنشئ علاقة توظيف جديدة لنفس الموظف وتحافظ على سجله ومستنداته التاريخية السابقة دون ازدواجية في الهوية.",
      employmentNumber: "الرقم الوظيفي الجديد (اختياري)",
      employmentNumberPlaceholder: "اتركه فارغاً للتوليد التلقائي...",
      employmentType: "نوع التوظيف",
      startDate: "تاريخ مباشرة العمل الجديد"
    }
  },

  attendance: {
    title: "سجل حركات الحضور والانصراف",
    subtitle: "راجع الحضور والانصراف وصحح الاستثناءات ضمن الفترة المحددة.",
    recordsTitle: "الحركات المسجلة",
    recordsDescription: "بيانات الحضور اليومية للموظفين ضمن الفترة المختارة.",
    tableAria: "جدول حركات الحضور والانصراف",
    emptyState: "لا توجد حركات حضور مسجلة في هذه الفترة الزمنية.",
    noRecordsDescription: "غيّر الفترة الزمنية لعرض حركات أخرى أو سجّل الحركة اليدوية عند توفر ملف موظف.",
    loadError: "تعذر تحميل سجل الحضور والانصراف. حاول مرة أخرى.",
    loading: "جارٍ التحميل...",
    missingCheckout: "لم يسجل انصراف",
    selectEmployeeHelper: "تسجيل الحضور يتطلب حساب موظف أو تحديد موظف",
    noEmploymentSelected: "يرجى تحديد موظف لتسجيل الحضور",
    filters: {
      fromDate: "من تاريخ",
      toDate: "إلى تاريخ"
    },
    actions: {
      punchIn: "تسجيل حضور يدوي",
      punchOut: "تسجيل انصراف يدوي",
      correct: "تصحيح البصمة"
    },
    columns: {
      date: "التاريخ",
      plannedSchedule: "المخطط",
      firstIn: "أول دخول",
      lastOut: "آخر خروج",
      workedHours: "ساعات العمل",
      status: "الحالة",
      lockState: "قفل الرواتب",
      actions: "الإجراءات"
    },
    correctionModal: {
      title: "تصحيح حركة حضور يدوي وتحديد موعد الخروج",
      recordDate: "تاريخ يوم العمل",
      checkoutTime: "وقت الانصراف الفعلي",
      reason: "سبب التصحيح / الاعتماد",
      reasonPlaceholder: "مثال: عطل في جهاز البصمة، مهمة خارجية..."
    }
  },

  leave: {
    title: "إدارة الإجازات السنوية والرسمية",
    subtitle: "تقديم الطلبات، متابعة الأرصدة المستهلكة، وموافقة أصحاب الصلاحية",
    typeRequired: "يرجى تحديد نوع الإجازة المطلوبة.",
    employeeRequired: "يرجى تحديد الموظف المعني بالطلب.",
    selectEmployeeHelper: "يتطلب تقديم طلب إجازة حساب موظف أو تحديد موظف",
    daysUnit: "يوم",
    tableAria: "جدول طلبات الإجازة",
    emptyState: "لا توجد طلبات إجازة في هذا السجل.",
    loading: "جارٍ التحميل...",
    columns: {
      from: "من تاريخ",
      to: "إلى تاريخ",
      chargedDays: "الأيام المخصومة",
      reason: "السبب والبيان",
      status: "الحالة",
      actions: "الإجراءات"
    },
    actions: {
      newRequest: "طلب إجازة جديدة",
      approve: "موافقة",
      cancel: "إلغاء الطلب"
    },
    newModal: {
      title: "تقديم طلب إجازة",
      leaveType: "نوع الإجازة",
      fromDate: "تاريخ البداية",
      toDate: "تاريخ النهاية",
      reasonOptional: "السبب / الملاحظات (اختياري)",
      reasonPlaceholder: "اكتب تفاصيل إضافية إن وجدت..."
    }
  },

  organization: {
    departmentsTitle: "الأقسام والإدارات",
    positionsTitle: "المسميات والوظائف",
    newDepartment: "إضافة قسم جديد",
    newPosition: "إضافة مسمى وظيفي",
    code: "الرمز الكودي",
    nameAr: "الاسم بالعربية",
    nameEn: "الاسم بالإنجليزية",
    status: "الحالة",
    noDepartments: "لا توجد أقسام مسجلة.",
    noPositions: "لا توجد مسميات وظيفية مسجلة."
  },

  selfservice: {
    title: "بوابة الخدمة الذاتية للموظف",
    noProfileLinked: "لا يوجد ملف موظف مرتبط بحساب المستخدم الحالي.",
    employmentNumber: "الرقم الوظيفي",
    employmentType: "نوع العقد",
    status: "الحالة الوظيفية"
  },

  manager: {
    leaveApprovalsTitle: "طلبات إجازة معلقة لفريقي",
    teamTitle: "أعضاء فريقي المباشر",
    from: "من تاريخ",
    to: "إلى تاريخ",
    chargedDays: "الأيام المطلوبة",
    reason: "السبب",
    actions: "الإجراءات",
    employeeNumber: "الرقم الوظيفي",
    name: "اسم الموظف",
    department: "القسم",
    position: "المسمى الوظيفي",
    status: "الحالة",
    approveAction: "موافقة",
    rejectAction: "رفض",
    noPendingApprovals: "لا توجد طلبات إجازة معلقة للموافقة.",
    noTeamMembers: "لا يوجد موظفون يتبعون إدارتك المباشرة حالياً."
  },

  reports: {
    title: "تقارير الموارد البشرية والامتثال",
    attendanceReport: "تقرير الحضور والانصراف التفصيلي",
    attendanceDesc: "بيان ساعات العمل والغياب والتأخير لجميع العاملين",
    leaveBalancesReport: "تقرير أرصدة وحركات الإجازات",
    leaveBalancesDesc: "موقف الرصيد السنوي المستهلك والمرحل لكل موظف",
    expiriesReport: "تقرير الوثائق والعقود المنتهية",
    expiriesDesc: "حصر الإقامات وجوازات السفر ورخص العمل التي قاربت على الانتهاء",
    exportExcel: "تصدير بصيغة إكسيل"
  },

  imports: {
    title: "ترحيل واستيراد بيانات الموارد البشرية",
    description: "استيراد بيانات الموظفين من الأنظمة القديمة مع فحص دقيق للبيانات ومطابقة السجلات قبل التثبيت النهائي.",
    previewAction: "معاينة وفحص الملف",
    commitAction: "تأكيد واستيراد نهائي",
    totalRows: "إجمالي الصفوف",
    importedRows: "صفوف جاهزة",
    rejectedRows: "صفوف مرفوضة",
    committedSuccess: "تم استيراد وتثبيت البيانات بنجاح في قاعدة البيانات",
    resultsTable: "نتائج الفحص والتحقق",
    rowRef: "مرجع الصف",
    status: "الحالة",
    issues: "الملاحظات والأخطاء",
    none: "لا يوجد"
  },

  errors: {
    generic: "حدث خطأ غير متوقع أثناء معالجة الطلب.",
    forbidden: "ليس لديك الصلاحية الكافية لإتمام هذا الإجراء.",
    employeeArabicNameRequired: "الاسم باللغة العربية مطلوب لإنشاء الموظف.",
    employeeNameTooLong: "اسم الموظف يتجاوز الحد الأقصى المسموح به.",
    employmentNumberInvalid: "الرقم الوظيفي غير صالح أو مكرر.",
    employmentDateRangeInvalid: "تاريخ بداية العمل يجب أن يسبق تاريخ نهايته.",
    leaveBalanceInsufficient: "رصيد الإجازات المتاح غير كافٍ لتغطية المدة المطلوبة.",
    leaveDatesOverlap: "تاريخ الإجازة يتعارض مع إجازة أخرى مسجلة مسبقاً لنفس الموظف.",
    attendancePayrollLocked: "سجل الحضور مغلق محاسبياً مع الرواتب ولا يمكن تعديله مباشرة.",
    attendanceCorrectionReasonRequired: "يجب إدخال سبب رسمي لتصحيح حركة الحضور.",
    assignmentDateInvalid: "تاريخ سريان التعيين أو الترقية غير صالح.",
    contractDateRangeInvalid: "تاريخ نهاية العقد يجب أن يكون بعد تاريخ بدايته.",
    contractAlreadyEnded: "العقد منتهٍ بالفعل ولا يمكن تعديله.",
    shiftBreakExceedsDuration: "وقت الراحة لا يمكن أن يتجاوز مدة الوردية الإجمالية.",
    disciplinaryAmountNotAllowed: "لا يجوز فرض مبالغ مالية مباشرة في الجزاءات؛ يتم احتساب الخصم عبر نظام الرواتب.",
    adjustmentPayrollInputInvalid: "بيانات تسوية الأثر المالي غير صحيحة.",
    insuranceAlreadyActive: "يوجد اشتراك تأميني نشط بالفعل لهذا النظام.",
    fileScanFailed: "فشل الفحص الأمني للمستند أو تم رفض الملف بواسطة برنامج الفحص.",
    employeeStale: "تم تحديث البيانات بواسطة مستخدم آخر، يرجى تحديث الصفحة والمحاولة مجدداً.",
    employeeProfileReasonRequired: "يجب إدخال سبب رسمي لتعديل بيانات الموظف."
  }
} as const;
