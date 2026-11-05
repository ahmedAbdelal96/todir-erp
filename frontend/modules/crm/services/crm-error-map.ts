import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedCrmError {
  readonly messageKey: string;
  readonly defaultMessage: string;
  readonly code?: string | undefined;
}

export function mapCrmError(error: unknown): MappedCrmError {
  let code: string | undefined;
  let status: number | undefined;
  let detail: string | undefined;
  let message: string | undefined;

  if (error instanceof ApiError) {
    code = error.problemDetails?.code || error.problemDetails?.title || error.message;
    status = error.status;
    detail = error.problemDetails?.detail;
    message = error.message;
  } else if (typeof error === "object" && error !== null) {
    const errObj = error as Record<string, any>;
    code = errObj.code || errObj.problemDetails?.code || errObj.problemDetails?.title || errObj.message;
    status = errObj.status;
    detail = errObj.problemDetails?.detail || errObj.detail;
    message = errObj.message;
  } else if (typeof error === "string") {
    code = error;
    message = error;
  }

  if (code) {
    switch (code) {
      case "crm.lead_not_found":
        return { messageKey: "errors.leadNotFound", defaultMessage: "العميل المحتمل غير موجود أو تم حذفه.", code };
      case "crm.lead_not_qualified":
        return { messageKey: "errors.leadNotQualified", defaultMessage: "يجب تأهيل العميل المحتمل أولاً قبل تحويله إلى فرصة.", code };
      case "crm.party_not_active":
        return { messageKey: "errors.partyNotActive", defaultMessage: "الطرف التجاري غير نشط في دليل الأطراف.", code };
      case "crm.party_required":
        return { messageKey: "errors.partyRequired", defaultMessage: "بيانات الطرف التجاري مطلوبة لإنشاء الحساب الجديد.", code };
      case "crm.opportunity_not_found":
        return { messageKey: "errors.opportunityNotFound", defaultMessage: "فرصة المبيعات غير موجودة.", code };
      case "crm.opportunity_closed":
        return { messageKey: "errors.opportunityClosed", defaultMessage: "الفرصة مغلقة مسبقاً ولا يمكن تعديلها.", code };
      case "crm.activity_not_found":
        return { messageKey: "errors.activityNotFound", defaultMessage: "النشاط أو المتابعة غير موجودة.", code };
      case "crm.activity_completed":
        return { messageKey: "errors.activityCompleted", defaultMessage: "النشاط مكتمل مسبقاً.", code };
      case "crm.pipeline_not_found":
        return { messageKey: "errors.pipelineNotFound", defaultMessage: "المسار البيعي غير موجود.", code };
      case "crm.stage_not_found":
        return { messageKey: "errors.stageNotFound", defaultMessage: "المرحلة البيعية غير موجودة.", code };
      case "crm.team_not_found":
        return { messageKey: "errors.teamNotFound", defaultMessage: "فريق المبيعات غير موجود.", code };
      case "crm.record_forbidden":
        return { messageKey: "errors.recordForbidden", defaultMessage: "ليس لديك صلاحية الوصول إلى هذا السجل أو تعيينه.", code };
      case "crm.import_empty":
        return { messageKey: "errors.importEmpty", defaultMessage: "ملف الاستيراد فارغ.", code };
      case "crm.import_header_required":
        return { messageKey: "errors.importHeaderRequired", defaultMessage: "ملف الاستيراد يجب أن يحتوي على صف العناوين الرئيسي.", code };
      case "crm.import_contact_columns_required":
        return { messageKey: "errors.importContactColumnsRequired", defaultMessage: "يجب تضمين أعمدة الاسم أو الهاتف أو البريد الإلكتروني.", code };
      case "crm.entity_mismatch":
        return { messageKey: "errors.entityMismatch", defaultMessage: "تعارض في المنشأة التابع لها السجل.", code };
      case "crm.stale_record":
      case "crm.concurrency_conflict":
        return { messageKey: "errors.concurrencyConflict", defaultMessage: "تم تعديل السجل بواسطة مستخدم آخر. يرجى التحديث وإعادة المحاولة.", code };
      default:
        break;
    }
  }

  if (status === 403) {
    return { messageKey: "errors.forbidden", defaultMessage: "ليس لديك الصلاحية الكافية لتنفيذ هذا الإجراء في CRM.", code };
  }
  if (status === 404) {
    return { messageKey: "errors.notFound", defaultMessage: "العنصر المطلوب غير موجود.", code };
  }

  if (detail || message) {
    return { messageKey: "errors.general", defaultMessage: detail || message || "حدث خطأ أثناء تنفيذ العملية.", code };
  }

  return { messageKey: "errors.unknown", defaultMessage: "حدث خطأ غير متوقع." };
}
