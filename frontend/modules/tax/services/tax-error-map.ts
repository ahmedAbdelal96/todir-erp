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
