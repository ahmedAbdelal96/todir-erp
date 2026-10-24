import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedTaxError {
  readonly code: string;
  readonly messageKey: string;
  readonly fallbackEn: string;
  readonly fallbackAr: string;
  readonly technicalDetail?: string | undefined;
}

const TAX_ERROR_DEFINITIONS: Record<string, { messageKey: string; fallbackEn: string; fallbackAr: string }> = {
  "tax.registration_missing": {
    messageKey: "errors.registrationMissing",
    fallbackEn: "Tax registration has not been configured for this legal entity.",
    fallbackAr: "لم يتم تكوين التسجيل الضريبي لهذا الكيان القانوني."
  },
  "tax.registration_not_found": {
    messageKey: "errors.registrationNotFound",
    fallbackEn: "The requested tax registration was not found.",
    fallbackAr: "لم يتم العثور على التسجيل الضريبي المطلوب."
  },
  "tax.registration_number_invalid": {
    messageKey: "errors.registrationNumberInvalid",
    fallbackEn: "Tax registration number is invalid.",
    fallbackAr: "رقم التسجيل الضريبي غير صالح."
  },
  "tax.product_classification_missing": {
    messageKey: "errors.productClassificationMissing",
    fallbackEn: "A product requires a valid tax classification.",
    fallbackAr: "يتطلب المنتج تصنيفاً ضريبياً صالحاً."
  },
  "tax.party_profile_missing": {
    messageKey: "errors.partyProfileMissing",
    fallbackEn: "A party tax profile is missing or incomplete.",
    fallbackAr: "الملف الضريبي للطرف التجاري مفقود أو غير مكتمل."
  },
  "tax.rule_missing": {
    messageKey: "errors.ruleMissing",
    fallbackEn: "No applicable tax rule matches this transaction context.",
    fallbackAr: "لا توجد قاعدة ضريبية مطابقة لمعاملات هذا السياق."
  },
  "tax.rule_conflict": {
    messageKey: "errors.ruleConflict",
    fallbackEn: "Conflicting tax rules need review before tax calculation can be used safely.",
    fallbackAr: "توجد قواعد ضريبية متعارضة تتطلب المراجعة قبل استخدام الحساب الضريبي."
  },
  "tax.rounding_policy_missing": {
    messageKey: "errors.roundingPolicyMissing",
    fallbackEn: "Tax rounding policy is not configured for this legal entity.",
    fallbackAr: "سياسة التقريب الضريبي غير مهيأة لهذا الكيان القانوني."
  },
  "tax.country_pack_missing": {
    messageKey: "errors.countryPackMissing",
    fallbackEn: "No Country Pack is installed or active for this jurisdiction.",
    fallbackAr: "لا توجد حزمة دولة مثبتة أو نشطة لهذا الاختصاص القضائي."
  },
  "tax.country_pack_incompatible": {
    messageKey: "errors.countryPackIncompatible",
    fallbackEn: "The Country Pack is incompatible with the current engine version.",
    fallbackAr: "حزمة الدولة غير متوافقة مع إصدار محرك الضرائب الحالي."
  },
  "tax.country_pack_evidence_missing": {
    messageKey: "errors.countryPackEvidenceMissing",
    fallbackEn: "Required legal evidence or statutory review is missing.",
    fallbackAr: "المستندات القانونية الإلزامية أو المراجعة التشريعية مفقودة."
  },
  "tax.rule_stack_cycle": {
    messageKey: "errors.stackCycle",
    fallbackEn: "These tax components depend on each other in a circular way.",
    fallbackAr: "مكونات الضريبة تعتمد على بعضها البعض بشكل دائري غير صالح."
  },
  "tax.stack_cycle": {
    messageKey: "errors.stackCycle",
    fallbackEn: "These tax components depend on each other in a circular way.",
    fallbackAr: "مكونات الضريبة تعتمد على بعضها البعض بشكل دائري غير صالح."
  },
  "tax.rule_set_not_editable": {
    messageKey: "errors.ruleSetNotEditable",
    fallbackEn: "Rules can be modified only while the Rule Set is in Draft status.",
    fallbackAr: "يمكن تعديل القواعد فقط عندما تكون مجموعة القواعد في حالة مسودة."
  },
  "tax.rule_set_source_locked": {
    messageKey: "errors.ruleSetSourceLocked",
    fallbackEn: "Statutory rules provided by a Country Pack cannot be edited directly.",
    fallbackAr: "لا يمكن تعديل القواعد التشريعية الصادرة عن حزمة الدولة مباشرة."
  },
  "tax.rule_locked": {
    messageKey: "errors.ruleLocked",
    fallbackEn: "This rule is locked by the Country Pack.",
    fallbackAr: "هذه القاعدة مقفلة بموجب حزمة الدولة."
  },
  "tax.effective_date_required": {
    messageKey: "errors.effectiveDateRequired",
    fallbackEn: "An effective date is required to access tax capability history.",
    fallbackAr: "تاريخ السريان مطلوب للوصول إلى سجل صلاحية الضريبة."
  },
  "tax.batch_size_invalid": {
    messageKey: "errors.batchSizeInvalid",
    fallbackEn: "Tax batch must contain between 1 and 500 items.",
    fallbackAr: "يجب أن تحتوي دفعة الضرائب على ما بين عنصر واحد و 500 عنصر."
  },
  "tax.batch_tenant_mismatch": {
    messageKey: "errors.batchTenantMismatch",
    fallbackEn: "All batch items must belong to the active tenant.",
    fallbackAr: "يجب أن تنتمي جميع عناصر الدفعة إلى نفس المستأجر النشط."
  }
};

export interface MappedImportOutcome {
  readonly outcomeKey: string;
  readonly messageKey: string;
  readonly fallbackEn: string;
  readonly fallbackAr: string;
  readonly isError: boolean;
  readonly technicalDetail?: string | undefined;
}

const IMPORT_OUTCOME_DEFINITIONS: Record<string, { messageKey: string; fallbackEn: string; fallbackAr: string; isError: boolean }> = {
  UnknownProduct: {
    messageKey: "importExport.outcomes.unknownProductMessage",
    fallbackEn: "Product was not found in Catalog.",
    fallbackAr: "لم يتم العثور على المنتج في الكتالوج.",
    isError: true
  },
  UnknownParty: {
    messageKey: "importExport.outcomes.unknownPartyMessage",
    fallbackEn: "Party was not found in Directory.",
    fallbackAr: "لم يتم العثور على الطرف التجاري في دليل الأطراف.",
    isError: true
  },
  AmbiguousReference: {
    messageKey: "importExport.outcomes.ambiguousReferenceMessage",
    fallbackEn: "The business reference matches more than one record.",
    fallbackAr: "المرجع التجاري يطابق أكثر من سجل.",
    isError: true
  },
  InvalidTaxCategory: {
    messageKey: "importExport.outcomes.invalidTaxCategoryMessage",
    fallbackEn: "The tax category is not valid for this profile.",
    fallbackAr: "الفئة الضريبية غير صالحة لهذا الملف.",
    isError: true
  },
  InvalidScheme: {
    messageKey: "importExport.outcomes.invalidSchemeMessage",
    fallbackEn: "The tax scheme is not valid for this profile.",
    fallbackAr: "النظام الضريبي غير صالح لهذا الملف.",
    isError: true
  },
  EffectiveDateOverlap: {
    messageKey: "importExport.outcomes.effectiveDateOverlapMessage",
    fallbackEn: "The effective date overlaps an existing profile version.",
    fallbackAr: "تاريخ السريان يتداخل مع إصدار قائم للملف.",
    isError: true
  },
  DuplicateInput: {
    messageKey: "importExport.outcomes.duplicateInputMessage",
    fallbackEn: "The import contains duplicate input data.",
    fallbackAr: "تحتوي بيانات الاستيراد على تكرار.",
    isError: true
  },
  LockedStatutoryContent: {
    messageKey: "importExport.outcomes.lockedStatutoryContentMessage",
    fallbackEn: "Statutory content is locked and cannot be changed by import.",
    fallbackAr: "المحتوى التشريعي مقفل ولا يمكن تغييره بالاستيراد.",
    isError: true
  },
  Conflict: {
    messageKey: "importExport.outcomes.conflictMessage",
    fallbackEn: "The imported profile conflicts with existing tax configuration.",
    fallbackAr: "الملف المستورد يتعارض مع التكوين الضريبي القائم.",
    isError: true
  },
  InvalidClassification: {
    messageKey: "importExport.outcomes.invalidClassificationMessage",
    fallbackEn: "Tax classification code is invalid.",
    fallbackAr: "رمز التصنيف الضريبي غير صالح.",
    isError: true
  },
  InvalidRate: {
    messageKey: "importExport.outcomes.invalidRateMessage",
    fallbackEn: "Tax rate or rate version is invalid.",
    fallbackAr: "نسبة الضريبة أو إصدار النسبة غير صالح.",
    isError: true
  },
  Duplicate: {
    messageKey: "importExport.outcomes.duplicateMessage",
    fallbackEn: "Duplicate record in import batch.",
    fallbackAr: "سجل مكرر في ملف الاستيراد.",
    isError: true
  },
  MissingRequiredField: {
    messageKey: "importExport.outcomes.missingRequiredFieldMessage",
    fallbackEn: "Mandatory import field is missing.",
    fallbackAr: "حقل إلزامي مفقود في بيانات الاستيراد.",
    isError: true
  },
  Create: {
    messageKey: "importExport.outcomes.createMessage",
    fallbackEn: "Valid for creation and committing.",
    fallbackAr: "صالح للاعتماد والحفظ كملف جديد.",
    isError: false
  },
  CreateNewVersion: {
    messageKey: "importExport.outcomes.createNewVersionMessage",
    fallbackEn: "Valid for creating a new effective version.",
    fallbackAr: "صالح لإنشاء إصدار جديد سارٍ.",
    isError: false
  }
};

const UNSAFE_TECHNICAL_DETAIL = /(?:password|passwd|secret|token|api[\s_-]*key|connection\s*string|server\s*=|host\s*=|user\s*id\s*=|uid\s*=|pwd\s*=|npgsql|sql(?:exception|command|state)|select\s+.+\s+from|insert\s+into|update\s+.+\s+set|delete\s+from|https?:\/\/|(?:[a-z]:\\|\/(?:home|users|var|tmp|workspace|app)\/)|^\s*at\s+.+\()/i;

export function sanitizeTechnicalDetail(detail: string | null | undefined): string | undefined {
  const normalized = detail?.trim();
  if (!normalized || normalized.length > 240 || UNSAFE_TECHNICAL_DETAIL.test(normalized)) {
    return undefined;
  }
  return normalized;
}

export function mapImportRowOutcome(outcome: string | undefined | null, technicalDetail?: string | null): MappedImportOutcome {
  const normalized = (outcome ?? "").trim();
  const matched = IMPORT_OUTCOME_DEFINITIONS[normalized];
  if (matched) {
    return {
      outcomeKey: `importExport.outcomes.${normalized}`,
      messageKey: matched.messageKey,
      fallbackEn: matched.fallbackEn,
      fallbackAr: matched.fallbackAr,
      isError: matched.isError,
      technicalDetail: sanitizeTechnicalDetail(technicalDetail)
    };
  }

  const isError =
    normalized.toLowerCase().includes("unknown") ||
    normalized.toLowerCase().includes("invalid") ||
    normalized.toLowerCase().includes("conflict") ||
    normalized.toLowerCase().includes("error") ||
    normalized.toLowerCase().includes("fail");

  return {
    outcomeKey: `importExport.outcomes.${normalized || "Error"}`,
    messageKey: isError ? "importExport.outcomes.genericErrorMessage" : "importExport.outcomes.genericSuccessMessage",
    fallbackEn: isError ? "Row validation failed." : "Valid for committing.",
    fallbackAr: isError ? "فشل التحقق من صحة السطر." : "صالح للاعتماد.",
    isError,
    technicalDetail: sanitizeTechnicalDetail(technicalDetail)
  };
}

export function mapTaxErrorCode(code: string | undefined | null, technicalDetail?: string): MappedTaxError {
  const normalized = (code ?? "").trim().toLowerCase();
  const matched = TAX_ERROR_DEFINITIONS[normalized];
  if (matched) {
    return {
      code: normalized,
      messageKey: matched.messageKey,
      fallbackEn: matched.fallbackEn,
      fallbackAr: matched.fallbackAr,
      technicalDetail
    };
  }

  return {
    code: normalized || "tax.unknown_error",
    messageKey: "errors.genericTaxError",
    fallbackEn: technicalDetail || "An unexpected tax engine error occurred.",
    fallbackAr: technicalDetail || "حدث خطأ غير متوقع في محرك الضرائب.",
    technicalDetail
  };
}

export function extractTaxError(error: unknown): MappedTaxError {
  if (error instanceof ApiError) {
    const code = error.problemDetails?.code ?? error.problemDetails?.title;
    return mapTaxErrorCode(code, error.message);
  }
  if (typeof error === "object" && error !== null && "code" in error) {
    return mapTaxErrorCode(String((error as { code: unknown }).code), (error as { message?: string }).message);
  }
  if (error instanceof Error) {
    return mapTaxErrorCode("tax.client_error", error.message);
  }
  return mapTaxErrorCode("tax.unknown_error", String(error));
}
