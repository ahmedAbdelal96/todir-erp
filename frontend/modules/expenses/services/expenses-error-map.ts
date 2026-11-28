export interface MappedExpenseError {
  readonly code: string;
  readonly messageKey: string;
  readonly defaultMessage: string;
}

const ERROR_MAP: Record<string, { messageKey: string; defaultMessage: string }> = {
  "expenses.expense_not_found": {
    messageKey: "errors.expenseNotFound",
    defaultMessage: "المصروف المطلوب غير موجود أو ليس لديك صلاحية الوصول إليه."
  },
  "expenses.invalid_amount": {
    messageKey: "errors.invalidAmount",
    defaultMessage: "المبلغ المالي أو اسم التاجر أو رمز العملة غير صالح."
  },
  "expenses.category_not_found": {
    messageKey: "errors.categoryNotFound",
    defaultMessage: "تصنيف المصروف المحدد غير متاح أو لم يتم نشره بعد."
  },
  "expenses.concurrent_change_or_not_editable": {
    messageKey: "errors.concurrentChange",
    defaultMessage: "تم تعديل هذا المصروف بواسطة مستخدم آخر أو أنه في حالة غير قابلة للتعديل."
  },
  "expenses.self_employee_not_found": {
    messageKey: "errors.selfEmployeeNotFound",
    defaultMessage: "تعذر مطابقة حساب المستخدم مع سجل موظف نشط في النظام."
  },
  "expenses.approval_request_invalid_state": {
    messageKey: "errors.approvalInvalidState",
    defaultMessage: "لا يمكن طلب الاعتماد إلا للمصروفات في حالة 'مقدم للاعتماد'."
  },
  "expenses.approval_decision_invalid": {
    messageKey: "errors.approvalDecisionInvalid",
    defaultMessage: "قرار الاعتماد غير صالح أو مكتمل مسبقاً."
  },
  "expenses.report_not_found": {
    messageKey: "errors.reportNotFound",
    defaultMessage: "تقرير المصروفات غير موجود."
  },
  "expenses.advance_not_found": {
    messageKey: "errors.advanceNotFound",
    defaultMessage: "سلفة العمل غير موجودة أو تم تسويتها بالكامل."
  },
  "expenses.card_transaction_not_found": {
    messageKey: "errors.cardNotFound",
    defaultMessage: "حركة بطاقة الشركة غير موجودة."
  },
  "planning.budget_approval_required": {
    messageKey: "errors.budgetApprovalRequired",
    defaultMessage: "يتطلب هذا المصروف موافقة استثناء موازنة قبل المتابعة."
  },
  "planning.budget_exceeded": {
    messageKey: "errors.budgetExceeded",
    defaultMessage: "تم حظر المصروف لعدم كفاية الرصيد المتاح في الموازنة المعتمدة."
  },
  "planning.budget_bucket_not_found": {
    messageKey: "errors.budgetBucketInvalid",
    defaultMessage: "وعاء الموازنة المحدد غير صالح أو غير نشط."
  }
};

export function mapExpenseError(error: { code?: string } | null | undefined): MappedExpenseError {
  const code = error?.code ?? "unknown";
  const mapped = ERROR_MAP[code];

  if (mapped) {
    return {
      code,
      messageKey: mapped.messageKey,
      defaultMessage: mapped.defaultMessage
    };
  }

  return {
    code,
    messageKey: "errors.unknown",
    defaultMessage: "حدث خطأ أثناء معالجة عملية المصروفات. يرجى المحاولة مرة أخرى."
  };
}
