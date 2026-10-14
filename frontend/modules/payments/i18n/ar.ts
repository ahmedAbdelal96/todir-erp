export const ar = {
  title: "المدفوعات", subtitle: "إدارة العمليات المالية والتحويلات المصرفية", newPayment: "تسجيل دفعة مالية جديدة",
  entry: {
    invalidAmount: "يجب إدخال مبلغ مالي صالح أكبر من الصفر.", submitFailed: "فشلت عملية صرف الدفعة المالية.", confirmTitle: "تأكيد صرف الدفعة المالية",
    unknownOutcomeTitle: "تنبيه: حالة التحويل غير مؤكدة", successTitle: "تم تنفيذ الدفعة بنجاح", title: "تسجيل دفعة مالية صادرة",
    confirmDescription: "يرجى مراجعة تفاصيل العملية المالية قبل الإرسال النهائي.", description: "صرف مالي لحساب: {{name}}", amount: "المبلغ المالي",
    paymentMethod: "طريقة الدفع", bankTransfer: "تحويل بنكي", cash: "نقداً / الصندوق", check: "شيك بنكي", card: "بطاقة دفع إلكتروني",
    sourceAccount: "حساب السحب / الصندوق", destinationAccount: "حساب الإيداع / المستفيد", referenceNumber: "الرقم المرجعي / رمز السند", notes: "الملاحظات والبيان",
    cancel: "إلغاء", continueToConfirmation: "متابعة إلى التأكيد", payee: "المستفيد / الطرف:", totalAmount: "المبلغ الإجمالي:",
    sourceAccountLabel: "حساب السحب:", referenceLabel: "الرقم المرجعي:", irreversibleWarning: "تنبيه: سيتم إرسال أمر الصرف المالي بصورة نهائية وغير قابلة للتراجع التلقائي.",
    editDetails: "تعديل البيانات", submitting: "جارٍ تنفيذ التحويل...", confirmAndSend: "تأكيد وإرسال الدفعة", connectionInterrupted: "انقطع الاتصال أثناء معالجة أمر الدفع:",
    unknownOutcomeDetails: "قد يكون التحويل البنكي قد نُفذ بالفعل لدى مزود الخدمة. تجنباً لتكرار الخصم، لا تضغط على زر الإرسال مرة أخرى. يرجى التحقق من حالة السند من خلال شاشة الحسابات.",
    closeAndCheckStatus: "إغلاق ومتابعة الحالة", successMessage: "تم تسجيل الدفعة بنجاح", paymentReference: "رقم العملية", done: "تم"
  }
} as const;
