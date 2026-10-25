export interface LedgerProblemDetail {
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly extensions?: {
    readonly code?: string;
    readonly [key: string]: unknown;
  };
}

const ERROR_MESSAGE_MAP: Record<string, { en: string; ar: string }> = {
  "ledger.entry_unbalanced": {
    en: "Journal entry debit and credit totals must be equal and greater than zero.",
    ar: "يجب أن يتساوى إجمالي المدين والدائن في القيد المحاسبي وأن يكون أكبر من الصفر."
  },
  "ledger.period_closed": {
    en: "The accounting period for this posting date is closed.",
    ar: "الفترة المحاسبية لهذا التاريخ مغلقة ولا يمكن الترحيل فيها."
  },
  "ledger.period_soft_closed": {
    en: "The accounting period is soft-closed. Only privileged adjustments are permitted.",
    ar: "الفترة المحاسبية مغلقة جزئياً. يُسمح فقط بالتسويات المصرح بها."
  },
  "ledger.lock_conflict": {
    en: "A financial lock is active for this period or module.",
    ar: "يوجد قفل مالي نشط لهذه الفترة أو الوحدة يمنع الترحيل."
  },
  "ledger.account_inactive": {
    en: "One or more selected accounts are archived or inactive.",
    ar: "واحد أو أكثر من الحسابات المحددة مؤرشف أو غير نشط."
  },
  "ledger.control_account_protected": {
    en: "Direct posting to subledger control accounts is prohibited.",
    ar: "لا يُسمح بالترحيل المباشر إلى حسابات المراقبة المرتبطة بالدفاتر المساعدة."
  },
  "ledger.control_account_manual_post_blocked": {
    en: "Manual journals cannot post directly to a protected control account.",
    ar: "لا يمكن ترحيل قيود اليومية اليدوية مباشرة إلى حساب مراقبة محمي."
  },
  "ledger.general_journal_transition_invalid": {
    en: "The general journal is not in the required lifecycle state for this transition.",
    ar: "مسودة القيد ليست في الحالة التشغيلية المطلوبة لتنفيذ هذا الإجراء."
  },
  "ledger.general_journal_not_editable": {
    en: "Only a draft general journal can be edited.",
    ar: "لا يمكن تعديل القيد إلا عندما يكون في حالة مسودة."
  },
  "ledger.general_journal_not_found": {
    en: "The general journal draft was not found.",
    ar: "مسودة قيد اليومية غير موجودة."
  },
  "ledger.dimension_invalid": {
    en: "The draft references an invalid dimension value.",
    ar: "القيد يشير إلى قيمة بعد تحليلي غير صالحة أو غير نشطة."
  },
  "ledger.opening_batch_already_processed": {
    en: "This opening balance batch has already been processed.",
    ar: "تمت معالجة وترحيل دفعة الأرصدة الافتتاحية هذه مسبقاً."
  },
  "ledger.opening_already_posted": {
    en: "This opening balance batch has already been posted.",
    ar: "تم ترحيل دفعة الأرصدة الافتتاحية هذه مسبقاً."
  },
  "ledger.sod_violation": {
    en: "The preparer cannot approve and post the same approved journal.",
    ar: "لا يمكن لمنشئ مسودة القيد اعتمادها وترحيلها بنفسه التزاماً بمبدأ الفصل بين المهام."
  },
  "ledger.reversal_date_invalid": {
    en: "Reversal date cannot precede original posting date.",
    ar: "تاريخ عكس القيد لا يمكن أن يكون سابقاً لتاريخ الترحيل الأصلي."
  },
  "ledger.opening_batch_unbalanced": {
    en: "Opening balance batch totals are unbalanced.",
    ar: "إجمالي الأرصدة الافتتاحية غير متوازن (المدين لا يساوي الدائن)."
  },
  "ledger.already_reversed": {
    en: "This journal entry has already been reversed.",
    ar: "تم عكس هذا القيد المحاسبي مسبقاً."
  },
  "ledger.draft_not_approved": {
    en: "Draft journal entry must be approved before posting.",
    ar: "يجب اعتماد مسودة القيد قبل إتمام الترحيل."
  },
  "ledger.fiscal_year_closed": {
    en: "The fiscal year is closed for new postings.",
    ar: "السنة المالية مغلقة أمام القيود الجديدة."
  }
};

export function mapLedgerError(error: unknown, language: "ar" | "en" = "ar"): string {
  if (!error) return "";

  if (typeof error === "string") return error;

  const problem = error as LedgerProblemDetail;
  const code = problem?.extensions?.code;
  if (code && ERROR_MESSAGE_MAP[code]) {
    return ERROR_MESSAGE_MAP[code][language];
  }

  if (problem?.title) {
    return problem.title;
  }

  if (problem?.detail) {
    return problem.detail;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return language === "ar" ? "حدث خطأ غير متوقع أثناء معالجة العملية المحاسبية." : "An unexpected error occurred while processing the accounting operation.";
}
