import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedInventoryError {
  readonly code: string;
  readonly messageKey: string;
  readonly fallbackEn: string;
  readonly fallbackAr: string;
  readonly technicalDetail?: string | undefined;
}

const INVENTORY_ERROR_DEFINITIONS: Record<string, { messageKey: string; fallbackEn: string; fallbackAr: string }> = {
  "inventory.insufficient_stock": {
    messageKey: "errors.insufficientStock",
    fallbackEn: "Insufficient on-hand stock for this operation.",
    fallbackAr: "الرصيد الفعلي في المخزن غير كافٍ لإتمام هذه العملية."
  },
  "inventory.insufficient_available_stock": {
    messageKey: "errors.insufficientAvailableStock",
    fallbackEn: "Insufficient available stock. Stock is reserved by other orders.",
    fallbackAr: "الرصيد المتاح غير كافٍ لوجود كميات محجوزة لطلبات أخرى."
  },
  "inventory.reservations_disabled": {
    messageKey: "errors.reservationsDisabled",
    fallbackEn: "Reservations are disabled for this product profile.",
    fallbackAr: "نظام حجز المخزون غير مفعّل لهذا المنتج."
  },
  "inventory.period_locked": {
    messageKey: "errors.periodLocked",
    fallbackEn: "The inventory operational period is locked for this date.",
    fallbackAr: "فترة العمليات المخزنية مغلقة لهذا التاريخ."
  },
  "inventory.before_operational_cutoff": {
    messageKey: "errors.beforeOperationalCutoff",
    fallbackEn: "Operation date is before the active operational cutoff date.",
    fallbackAr: "تاريخ العملية يسبق تاريخ الإغلاق الفعلي المعتمد للمخزون."
  },
  "inventory.lot_required": {
    messageKey: "errors.lotRequired",
    fallbackEn: "A valid lot/batch is required for this product.",
    fallbackAr: "يلزم تحديد رقم التشغيلة/الدفعة لهذا المنتج."
  },
  "inventory.serial_required": {
    messageKey: "errors.serialRequired",
    fallbackEn: "A valid serial number is required for this product.",
    fallbackAr: "يلزم تحديد الرقم التسلسلي الدقيق لهذه القطعة."
  },
  "inventory.traceability_not_enabled": {
    messageKey: "errors.traceabilityNotEnabled",
    fallbackEn: "Lot or serial tracking is not enabled for this product.",
    fallbackAr: "التتبع برقم التشغيلة أو السيريال غير مفعل لهذا المنتج."
  },
  "inventory.serial_quantity_must_be_one": {
    messageKey: "errors.serialQuantityMustBeOne",
    fallbackEn: "Serial-tracked movements must have a quantity of exactly 1.",
    fallbackAr: "حركات المنتجات المسلسلة يجب أن تكون بكمية 1 فقط لكل قطعة."
  },
  "inventory.receipt_cost_required": {
    messageKey: "errors.receiptCostRequired",
    fallbackEn: "A unit cost and currency are required for perpetual inventory receipts.",
    fallbackAr: "تكلفة الوحدة والعملة مطلوبة لاستلام المخزون بنظام الجرد المستمر."
  },
  "inventory.issue_cost_is_calculated": {
    messageKey: "errors.issueCostIsCalculated",
    fallbackEn: "Issue cost is calculated automatically by the valuation engine.",
    fallbackAr: "تكلفة الصرف تُحسب آلياً بواسطة محرك التقييم ولا يجوز إدخالها يدوياً."
  },
  "inventory.cost_not_allowed": {
    messageKey: "errors.costNotAllowed",
    fallbackEn: "Cost entries are not allowed for non-valuated products.",
    fallbackAr: "إدخال التكلفة غير مسموح للمنتجات غير المقيمة مالياً."
  },
  "inventory.profile_not_found": {
    messageKey: "errors.profileNotFound",
    fallbackEn: "This product is not configured for inventory operations yet.",
    fallbackAr: "الصنف غير مهيأ للمخزون بعد. جهّز إعدادات المخزون للصنف ثم أعد تأكيد الاستلام."
  },
  "inventory.catalog_variant_not_found": {
    messageKey: "errors.catalogVariantNotFound",
    fallbackEn: "The selected product is no longer available in the current legal entity.",
    fallbackAr: "الصنف المحدد غير متاح حاليًا لهذه المنشأة. اختر الصنف من جديد."
  },
  "inventory.uom_conversion_not_available": {
    messageKey: "errors.uomConversionNotAvailable",
    fallbackEn: "The selected product has no valid unit-of-measure conversion.",
    fallbackAr: "لا توجد وحدة قياس أساسية صالحة لهذا الصنف. استكمل إعداد وحدة القياس أولًا."
  },
  "inventory.quantity_invalid": {
    messageKey: "errors.quantityInvalid",
    fallbackEn: "Movement quantity must be greater than zero.",
    fallbackAr: "الكمية يجب أن تكون أكبر من الصفر."
  },
  "inventory.posting_before_operation_invalid": {
    messageKey: "errors.postingBeforeOperationInvalid",
    fallbackEn: "Posting date cannot be earlier than operational date.",
    fallbackAr: "تاريخ الترحيل لا يمكن أن يسبق تاريخ العملية الفعلي."
  },
  "inventory.product_not_stock_tracked": {
    messageKey: "errors.productNotStockTracked",
    fallbackEn: "This product is marked as non-stocked.",
    fallbackAr: "هذا المنتج معرف كخدمة أو صنف غير مخزني."
  },
  "inventory.costing_method_required": {
    messageKey: "errors.costingMethodRequired",
    fallbackEn: "A costing method (FIFO or Moving Average) is required for perpetual valuation.",
    fallbackAr: "طريقة تقييم التكلفة (الوارد أولاً أو المتوسط المرجح) مطلوبة لنظام التقييم المستمر."
  },
  "inventory.location_cycle": {
    messageKey: "errors.locationCycle",
    fallbackEn: "A location cannot be moved under itself or create a circular hierarchy.",
    fallbackAr: "لا يمكن نقل الموقع تحت نفسه أو تكوين حلقة دائرية في الهيكل."
  },
  "inventory.location_not_found": {
    messageKey: "errors.locationNotFound",
    fallbackEn: "The requested inventory location was not found.",
    fallbackAr: "موقع التخزين المطلوب غير موجود."
  },
  "inventory.location_not_usable": {
    messageKey: "errors.locationNotUsable",
    fallbackEn: "The selected location is inactive or cannot receive stock.",
    fallbackAr: "الموقع الداخلي المحدد غير نشط أو لا يقبل الاستلام. اختر موقعًا صالحًا."
  },
  "inventory.warehouse_inactive": {
    messageKey: "errors.warehouseInactive",
    fallbackEn: "Warehouse is not active and cannot accept stock operations.",
    fallbackAr: "المستودع غير نشط ولا يقبل حركات مخزنية."
  },
  "inventory.warehouse_has_stock": {
    messageKey: "errors.warehouseHasStock",
    fallbackEn: "Cannot archive warehouse because it still contains stock balances.",
    fallbackAr: "لا يمكن أرشفة المستودع لوجود أرصدة مخزنية قائمة فيه."
  },
  "inventory.transfer_not_found": {
    messageKey: "errors.transferNotFound",
    fallbackEn: "The requested transfer was not found.",
    fallbackAr: "طلب تحويل المخزون المطلوب غير موجود."
  },
  "inventory.transfer_already_completed": {
    messageKey: "errors.transferAlreadyCompleted",
    fallbackEn: "This transfer has already been completed.",
    fallbackAr: "تم استلام هذا التحويل بالكامل بالفعل."
  },
  "inventory.physical_count_not_found": {
    messageKey: "errors.physicalCountNotFound",
    fallbackEn: "The physical count session was not found.",
    fallbackAr: "جلسة الجرد الفعلي غير موجودة."
  },
  "inventory.physical_count_already_posted": {
    messageKey: "errors.physicalCountAlreadyPosted",
    fallbackEn: "This count session has already been posted.",
    fallbackAr: "تم ترحيل جلسة الجرد الفعلي هذه بالفعل."
  },
  "inventory.adjustment_request_not_found": {
    messageKey: "errors.adjustmentRequestNotFound",
    fallbackEn: "The adjustment request was not found.",
    fallbackAr: "طلب التسوية المخزنية غير موجود."
  },
  "inventory.adjustment_separation_of_duties": {
    messageKey: "errors.adjustmentSeparationOfDuties",
    fallbackEn: "Separation of duties requires a different user to approve this adjustment.",
    fallbackAr: "مبدأ فصل المهام يمنع منشئ طلب التسوية من اعتماده بنفسه."
  },
  "inventory.operation_already_reversed": {
    messageKey: "errors.operationAlreadyReversed",
    fallbackEn: "This inventory operation has already been reversed.",
    fallbackAr: "تم عكس هذه العملية المخزنية مسبقاً."
  },
  "inventory.serial_duplicate": {
    messageKey: "errors.serialDuplicate",
    fallbackEn: "Serial number already exists in active inventory.",
    fallbackAr: "الرقم التسلسلي مسجل مسبقاً في المخزون النشط."
  },
  "inventory.negative_stock_disallowed": {
    messageKey: "errors.negativeStockDisallowed",
    fallbackEn: "Negative stock is disallowed by entity policy.",
    fallbackAr: "الصرف بالسالب غير مسموح بموجب سياسة الكيان القانوني."
  }
};

export function mapInventoryError(error: unknown): MappedInventoryError {
  if (error instanceof ApiError) {
    const code = error.problemDetails?.code ?? (error as unknown as { code?: string }).code;
    if (code && INVENTORY_ERROR_DEFINITIONS[code]) {
      const def = INVENTORY_ERROR_DEFINITIONS[code]!;
      return {
        code,
        messageKey: def.messageKey,
        fallbackEn: def.fallbackEn,
        fallbackAr: def.fallbackAr,
        technicalDetail: error.message
      };
    }

    if (error.status === 403) {
      return {
        code: "inventory.forbidden",
        messageKey: "errors.forbidden",
        fallbackEn: "You do not have permission to perform this inventory operation.",
        fallbackAr: "ليست لديك الصلاحية المطلوبة لتنفيذ هذه العملية المخزنية.",
        technicalDetail: error.message
      };
    }

    return {
      code: code ?? `http_${error.status}`,
      messageKey: "errors.generic",
      fallbackEn: error.message || "An error occurred during the inventory operation.",
      fallbackAr: "حدث خطأ أثناء تنفيذ العملية المخزنية.",
      technicalDetail: error.message
    };
  }

  const rawMessage = error instanceof Error ? error.message : String(error);
  return {
    code: "inventory.unknown",
    messageKey: "errors.generic",
    fallbackEn: rawMessage || "An unexpected error occurred.",
    fallbackAr: "حدث خطأ غير متوقع في العملية المخزنية.",
    technicalDetail: rawMessage
  };
}
