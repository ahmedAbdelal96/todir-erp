export interface MappedPayrollError {
  readonly code: string;
  readonly messageKey: string;
  readonly defaultMessage: string;
}

export function mapPayrollError(error: unknown): MappedPayrollError {
  if (!error || typeof error !== "object") {
    return {
      code: "payroll.unknown_error",
      messageKey: "errors.generic",
      defaultMessage: "حدث خطأ غير متوقع أثناء معالجة عملية الرواتب."
    };
  }

  const err = error as {
    code?: string;
    message?: string;
    title?: string;
    status?: number;
    extensions?: { code?: string };
  };

  const rawCode = err.extensions?.code ?? err.code ?? err.title ?? "";

  const errorMappings: Record<string, { messageKey: string; defaultMessage: string }> = {
    "payroll.group_not_found": {
      messageKey: "errors.groupNotFound",
      defaultMessage: "مجموعة الرواتب المحددة غير موجودة أو تم حذفها."
    },
    "payroll.run_not_found": {
      messageKey: "errors.runNotFound",
      defaultMessage: "مسير الرواتب المحدد غير موجود."
    },
    "payroll.run_already_finalized": {
      messageKey: "errors.runAlreadyFinalized",
      defaultMessage: "لا يمكن تعديل أو احتساب هذا المسير لأنه تم اعتماده نهائياً."
    },
    "payroll.run_not_ready": {
      messageKey: "errors.runNotReady",
      defaultMessage: "المسير غير جاهز للاعتماد النهائي؛ تأكد من احتسابه بالكامل وخلوه من الأخطاء."
    },
    "payroll.run_blocking_exceptions": {
      messageKey: "errors.runBlockingExceptions",
      defaultMessage: "توجد أخطاء احتساب مانعة للاعتماد في هذا المسير، يرجى مراجعة تفاصيل الموظفين."
    },
    "payroll.run_approval_required": {
      messageKey: "errors.runApprovalRequired",
      defaultMessage: "يتطلب مسير الرواتب موافقة رسمية قبل التثبيت والاعتماد النهائي."
    },
    "payroll.hr_snapshot_missing": {
      messageKey: "errors.hrSnapshotMissing",
      defaultMessage: "لم يتم التقاط لقطة بيانات الموارد البشرية (HR Snapshot) بعد."
    },
    "payroll.hr_snapshot_immutable": {
      messageKey: "errors.hrSnapshotImmutable",
      defaultMessage: "لقطة بيانات الموارد البشرية غير قابلة للتغيير بعد تسجيلها."
    },
    "payroll.settings_version_conflict": {
      messageKey: "errors.settingsVersionConflict",
      defaultMessage: "حدث تعارض في إصدار الإعدادات الضريبية/التأمينية، يرجى تحديث الصفحة والمحاولة مجدداً."
    },
    "payroll.settings_historical_change_blocked": {
      messageKey: "errors.settingsHistoricalChangeBlocked",
      defaultMessage: "لا يمكن تغيير الإعدادات الضريبية بأثر رجعي لفترة رواتب تم اعتمادها نهائياً."
    },
    "payroll.receivable_not_found": {
      messageKey: "errors.receivableNotFound",
      defaultMessage: "السلفة أو القرض المحدد غير موجود."
    },
    "payroll.receivable_not_approved": {
      messageKey: "errors.receivableNotApproved",
      defaultMessage: "يجب الموافقة على السلفة أو القرض أولاً قبل طلب الصرف."
    },
    "payroll.loan_action_invalid": {
      messageKey: "errors.loanActionInvalid",
      defaultMessage: "الإجراء المطلوب على القرض غير صالح في حالته الحالية."
    },
    "payroll.import_validation_failed": {
      messageKey: "errors.importValidationFailed",
      defaultMessage: "فشل التحقق من صحة ملف الاستيراد؛ توجد صفوف تحتوي على بيانات غير صالحة."
    },
    "payroll.retro_source_run_required": {
      messageKey: "errors.retroSourceRunRequired",
      defaultMessage: "يجب تحديد مسير الرواتب الأصلي المعتمد لاحتساب الفروقات الرجعية."
    },
    "payroll.settlement_employee_required": {
      messageKey: "errors.settlementEmployeeRequired",
      defaultMessage: "يجب تحديد الموظف المعني لإجراء مخالصة نهاية الخدمة."
    }
  };

  for (const [codePrefix, mapping] of Object.entries(errorMappings)) {
    if (rawCode === codePrefix || rawCode.startsWith(codePrefix)) {
      return {
        code: rawCode,
        messageKey: mapping.messageKey,
        defaultMessage: mapping.defaultMessage
      };
    }
  }

  return {
    code: rawCode || "payroll.operation_failed",
    messageKey: "errors.operationFailed",
    defaultMessage: err.message || "تعذر إتمام العملية المطلوبة، يرجى مراجعة المدخلات والمحاولة لاحقاً."
  };
}
