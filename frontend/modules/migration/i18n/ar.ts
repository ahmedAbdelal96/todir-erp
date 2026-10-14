export const ar = {
  title: "معالج الاستيراد",
  wizard: {
    title: "معالج استيراد بيانات الأطراف التجارية", uploadTitle: "رفع الملف", uploadDescription: "تحديد ملف البيانات (CSV / Excel)",
    mappingTitle: "مطابقة الحقول", mappingDescription: "ربط أعمدة الملف بحقول النظام", validationTitle: "الفحص والتجربة",
    validationStepDescription: "فحص صحة البيانات وتشغيل تجريبي", executionTitle: "تنفيذ الاستيراد", executionDescription: "استيراد السجلات إلى قاعدة البيانات",
    displayName: "الاسم التجاري / اسم المنشأة", legalName: "الاسم القانوني", taxId: "الرقم الضريبي (VAT)", partyType: "نوع الطرف (منشأة / فرد)", email: "البريد الإلكتروني", phone: "رقم الهاتف",
    selectFile: "اختر ملف البيانات المراد استيراده", supportedFormats: "الصيغ المدعومة: CSV، Excel (.xlsx). الحد الأقصى لحجم الملف 25 ميجابايت.", selectedFile: "الملف المحدد", records: "سجل",
    mapColumnsTitle: "ربط أعمدة الملف بحقول النظام", mapColumnsDescription: "قم بمطابقة كل عمود من ملف الاستيراد مع الحقل المقابل له في دليل الأطراف.",
    sourceColumn: "العمود في الملف", targetField: "حقل النظام المقابل", fieldStatus: "حالة الحقل", mapColumn: "مطابقة {{column}}", ignoreColumn: "— تجاهل هذا العمود —",
    requiredField: "حقل إلزامي *", optionalField: "اختياري", validationResults: "نتائج الفحص والتشغيل التجريبي (Dry Run)",
    validationDescription: "تم فحص بنية البيانات ومطابقتها لقواعد التحقق قبل كتابة أي سجل فعلي في قاعدة البيانات.", totalRecords: "إجمالي السجلات",
    validRecords: "سجلات صالحة للاستيراد", invalidRecords: "سجلات تحتوي على أخطاء", sampleErrors: "سجل الأخطاء المكتشفة (عينة):",
    sampleErrorTax: "السطر {{row}}: الرقم الضريبي غير صالح (يجب أن يتكون من 15 رقماً ويبدأ بـ 3).", sampleErrorEmail: "السطر {{row}}: صيغة البريد الإلكتروني غير صحيحة.",
    sampleErrorName: "السطر {{row}}: الاسم التجاري فارغ وهو حقل إلزامي.", completeTitle: "اكتمل تنفيذ عملية الاستيراد الجزئي",
    completeDescription: "تم ترحيل {{created}} سجلاً بنجاح إلى دليل الأطراف التجارية. تم استبعاد {{errors}} سجلاً مخالفاً وإصدار تقرير تسوية بالأخطاء.",
    downloadErrors: "تحميل تقرير الأخطاء المستبعدة (CSV)", backToDirectory: "العودة إلى دليل الأطراف"
  }
} as const;
