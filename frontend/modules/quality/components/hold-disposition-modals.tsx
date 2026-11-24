import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";
import {
  useCreateRestrictionForInspection,
  useReleaseRestriction,
  useCreateDisposition,
  useApproveDisposition,
  useQualityInspections,
  useQualityInspection
} from "../hooks/use-quality-queries.ts";
import { useWarehouses, useLocations, useStockBalances } from "../../inventory/public.ts";

// --------------------------------------------------------------------------
// 1. Create Restriction (Quality Hold) Modal
// --------------------------------------------------------------------------

interface CreateRestrictionModalProps {
  inspectionId?: string | null;
  defaultInspectionId?: string | null;
  defaultQuantity?: number | null;
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateRestrictionModal({
  inspectionId,
  defaultInspectionId,
  defaultQuantity,
  legalEntityId,
  isOpen,
  onClose
}: CreateRestrictionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createRestrictionMutation = useCreateRestrictionForInspection();
  const [inspectionCursor, setInspectionCursor] = useState<string | null>(null);
  const { data: inspectionsData, isLoading: inspectionsLoading } = useQualityInspections(
    legalEntityId,
    null,
    null,
    50,
    inspectionCursor
  );

  const initialInspection = inspectionId || defaultInspectionId || "";
  const [selectedInspectionId, setSelectedInspectionId] = useState(initialInspection);
  const [inspectionSearch, setInspectionSearch] = useState("");
  const singleInspectionQuery = useQualityInspection(selectedInspectionId || null);

  const matchedInspection = inspectionsData?.items?.find((i) => i.id === selectedInspectionId.trim());
  const currentInspection = matchedInspection || singleInspectionQuery.data;

  const [holdScope, setHoldScope] = useState<"physical" | "administrative">("physical");
  const [restrictionType, setRestrictionType] = useState("QualityHold");
  const [reasonCode, setReasonCode] = useState("RECEIVING_INSPECTION_HOLD");
  const [quantity, setQuantity] = useState(defaultQuantity ? String(defaultQuantity) : "");

  const { data: warehouses = [] } = useWarehouses(legalEntityId);
  const [warehouseId, setWarehouseId] = useState<string>("");
  const { data: locations = [] } = useLocations(legalEntityId, warehouseId || undefined);
  const [locationId, setLocationId] = useState<string>("");

  const isPhysical = holdScope === "physical";
  const missingProductVariant = isPhysical && Boolean(currentInspection) && !currentInspection?.productVariantId;

  // Query actual stock balances for the inspection's product variant
  const { data: stockBalances = [] } = useStockBalances(
    legalEntityId,
    { productVariantId: currentInspection?.productVariantId || undefined },
    Boolean(isPhysical && currentInspection?.productVariantId)
  );

  // Filter warehouses and locations to those holding available stock for this variant
  const eligibleWarehouseIds = React.useMemo(() => {
    if (!isPhysical || !currentInspection?.productVariantId || stockBalances.length === 0) return null;
    return new Set(stockBalances.filter((b) => b.stockStatus === "Available" && b.availableBaseQuantity > 0).map((b) => b.warehouseId));
  }, [isPhysical, currentInspection?.productVariantId, stockBalances]);

  const filteredWarehouses = React.useMemo(() => {
    if (!eligibleWarehouseIds || eligibleWarehouseIds.size === 0) return warehouses;
    return warehouses.filter((w) => eligibleWarehouseIds.has(w.id));
  }, [warehouses, eligibleWarehouseIds]);

  const eligibleLocationIds = React.useMemo(() => {
    if (!isPhysical || !warehouseId || !currentInspection?.productVariantId || stockBalances.length === 0) return null;
    return new Set(
      stockBalances
        .filter((b) => b.warehouseId === warehouseId && b.stockStatus === "Available" && b.availableBaseQuantity > 0)
        .map((b) => b.locationId)
    );
  }, [isPhysical, warehouseId, currentInspection?.productVariantId, stockBalances]);

  const filteredLocations = React.useMemo(() => {
    if (!eligibleLocationIds || eligibleLocationIds.size === 0) return locations;
    return locations.filter((loc) => eligibleLocationIds.has(loc.id));
  }, [locations, eligibleLocationIds]);

  const availableStockInSelection = React.useMemo(() => {
    if (!isPhysical || !currentInspection?.productVariantId || stockBalances.length === 0) return 0;
    return stockBalances
      .filter((b) => b.stockStatus === "Available" && (!warehouseId || b.warehouseId === warehouseId) && (!locationId || b.locationId === locationId))
      .reduce((sum, b) => sum + (b.availableBaseQuantity || 0), 0);
  }, [isPhysical, currentInspection?.productVariantId, stockBalances, warehouseId, locationId]);

  // Update inspection when initial prop or inspectionsData load - zero test UUID fixtures
  React.useEffect(() => {
    if (initialInspection) {
      setSelectedInspectionId(initialInspection);
    } else if (inspectionsData?.items && inspectionsData.items.length > 0 && !selectedInspectionId) {
      const preferred =
        inspectionsData.items.find((i) => Boolean(i.productVariantId)) ||
        inspectionsData.items[0];
      if (preferred) {
        setSelectedInspectionId(preferred.id);
        if (!quantity && preferred.lotQuantity) {
          setQuantity(String(preferred.lotQuantity));
        }
      }
    }
  }, [initialInspection, inspectionsData, selectedInspectionId, quantity]);

  const handleInspectionChange = (inspId: string) => {
    setSelectedInspectionId(inspId);
    const found = inspectionsData?.items?.find((i) => i.id === inspId);
    if (found && !quantity && found.lotQuantity) {
      setQuantity(String(found.lotQuantity));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const finalInspectionId = selectedInspectionId.trim();
    const qty = isPhysical ? Number(quantity) : (quantity ? Number(quantity) : null);
    if (!finalInspectionId || !reasonCode.trim()) return;
    if (isPhysical && (!qty || qty <= 0 || missingProductVariant)) return;

    try {
      await createRestrictionMutation.mutateAsync({
        inspectionId: finalInspectionId,
        legalEntityId,
        request: {
          restrictionType,
          reasonCode: reasonCode.trim(),
          quantity: isPhysical ? qty : null,
          inspectionId: finalInspectionId || null,
          productVariantId: isPhysical ? (currentInspection?.productVariantId || null) : null,
          warehouseId: isPhysical ? (warehouseId || null) : null,
          locationId: isPhysical ? (locationId || null) : null
        }
      });
      onClose();
    } catch (err) {
      console.error("Failed to create restriction:", err);
    }
  };

  const allInspectionOptions = (inspectionsData?.items ?? []).map((ins) => ({
    id: ins.id,
    label: `${ins.number} - ${t("eventTypes." + ins.eventType, ins.eventType)} (${ins.lotQuantity ?? 0})`
  }));

  const filteredInspectionOptions = inspectionSearch.trim()
    ? allInspectionOptions.filter((opt) => opt.label.toLowerCase().includes(inspectionSearch.toLowerCase()))
    : allInspectionOptions;

  const warehouseOptions = filteredWarehouses.map((w) => ({
    id: w.id,
    label: `${w.code} - ${w.name}`
  }));

  const locationOptions = filteredLocations.map((loc) => ({
    id: loc.id,
    label: `${loc.code} - ${loc.name}`
  }));

  const isSaveDisabled =
    createRestrictionMutation.isPending ||
    !selectedInspectionId.trim() ||
    !reasonCode.trim() ||
    missingProductVariant ||
    (isPhysical && (!quantity || Number(quantity) <= 0));

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createRestriction", "حظر واحتجاز الدفعة (Quality Hold)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        {!initialInspection && allInspectionOptions.length > 5 && (
          <TextField
            label={t("fields.searchInspection", "بحث في أوامر الفحص")}
            value={inspectionSearch}
            onChange={setInspectionSearch}
            placeholder={t("fields.searchInspectionPlaceholder", "ابحث برقم الفحص أو نوع الحدث...")}
          />
        )}
        {!initialInspection && filteredInspectionOptions.length > 0 && (
          <Select
            label={t("navigation.inspections", "أمر الفحص المرتبط بالاحتجاز")}
            selectedKey={selectedInspectionId}
            onSelectionChange={(key) => handleInspectionChange(String(key))}
            options={filteredInspectionOptions}
          />
        )}

        <Select
          label={t("fields.holdScope", "نطاق وطبيعة الحظر")}
          selectedKey={holdScope}
          onSelectionChange={(key) => setHoldScope(key as "physical" | "administrative")}
          options={[
            { id: "physical", label: t("fields.physicalHoldBound", "حظر مخزني فعلي (نقل الرصيد إلى رصيد حظر الجودة)") },
            { id: "administrative", label: t("fields.administrativeHold", "حظر إداري ومستندي (منع المعاملات دون قفل مخزني)") }
          ]}
        />

        {currentInspection && (
          <div
            data-testid="inspection-hold-context-banner"
            style={{
              padding: "10px 12px",
              backgroundColor: isPhysical ? "#eff6ff" : "#fefce8",
              border: `1px solid ${isPhysical ? "#bfdbfe" : "#fef08a"}`,
              borderRadius: "6px",
              fontSize: "12px",
              color: isPhysical ? "#1e3a8a" : "#713f12"
            }}
          >
            <div><strong>{t("fields.inspectionNumber", "رقم أمر الفحص")}:</strong> <BdiText>{currentInspection.number}</BdiText></div>
            <div style={{ marginTop: "4px" }}>
              <strong>{t("fields.inventoryScope", "نطاق حظر المخزون")}:</strong>{" "}
              {isPhysical
                ? t("fields.physicalHoldBound", "حظر مخزني فعلي مرتبط بدفعة وصنف الفحص المعتمد")
                : t("fields.administrativeHold", "حظر إداري ومستندي (بدون قفل مخزني فعلي)")}
            </div>
            {currentInspection.lotQuantity != null && (
              <div style={{ marginTop: "4px" }}>
                <strong>{t("fields.lotQuantity", "كمية الدفعة")}:</strong> <BdiText>{String(currentInspection.lotQuantity)}</BdiText>
              </div>
            )}
            <div style={{ marginTop: "4px" }}>
              <strong>{t("fields.expectedEffect", "الأثر التشغيلي المتوقع")}:</strong>{" "}
              {isPhysical
                ? t("fields.physicalHoldEffect", "سيتم نقل الكمية المحددة من الرصيد المتاح (Available) إلى رصيد حظر الجودة المخصص (QualityHold).")
                : t("fields.administrativeHoldEffect", "حظر مستندي إداري يمنع استكمال الإجراءات لحين رفع الحظر دون تحريك أرصدة المخزون.")}
            </div>
          </div>
        )}

        {missingProductVariant && (
          <div
            role="alert"
            data-testid="hold-missing-variant-alert"
            style={{
              padding: "10px 12px",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              borderRadius: "6px",
              fontSize: "12px",
              color: "#991b1b"
            }}
          >
            <strong>{t("fields.physicalHoldMissingVariantTitle", "تنبيه: لا يمكن تطبيق الحظر الفعلي")}</strong>
            <p style={{ margin: "4px 0 0 0" }}>
              {t("fields.physicalHoldMissingVariantMsg", "أمر الفحص المختار غير مرتبط بصنف مخزني (Product Variant). الحظر الفعلي يتطلب صنفاً مخزنياً لحجز الرصيد. يرجى اختيار فحص مرتبط بصنف أو تغيير نطاق الحظر إلى 'حظر إداري ومستندي'.")}
            </p>
          </div>
        )}

        {isPhysical && currentInspection?.productVariantId && stockBalances.length > 0 && (
          <div
            data-testid="available-stock-indicator"
            style={{
              padding: "8px 12px",
              backgroundColor: "#f0fdf4",
              border: "1px solid #bbf7d0",
              borderRadius: "6px",
              fontSize: "12px",
              color: "#166534"
            }}
          >
            <strong>{t("fields.availableStock", "الرصيد المتاح القابل للحظر")}:</strong>{" "}
            <BdiText>{String(availableStockInSelection)}</BdiText>
          </div>
        )}

        {isPhysical && warehouseOptions.length > 0 && (
          <Select
            label={t("fields.warehouse", "المستودع (اختياري / تلقائي من الرصيد)")}
            selectedKey={warehouseId}
            onSelectionChange={(key) => {
              setWarehouseId(String(key));
              setLocationId("");
            }}
            options={[{ id: "", label: t("fields.autoSelectWarehouse", "تحديد تلقائي من الرصيد المتاح") }, ...warehouseOptions]}
          />
        )}
        {isPhysical && warehouseId && locationOptions.length > 0 && (
          <Select
            label={t("fields.location", "موقع التخزين داخل المستودع")}
            selectedKey={locationId}
            onSelectionChange={(key) => setLocationId(String(key))}
            options={[{ id: "", label: t("fields.autoSelectLocation", "تحديد تلقائي من الموقع الافتراضي") }, ...locationOptions]}
          />
        )}

        <Select
          label={t("fields.restrictionType", "نوع الحظر والاحتجاز")}
          selectedKey={restrictionType}
          onSelectionChange={(key) => setRestrictionType(String(key))}
          options={[
            { id: "QualityHold", label: t("restrictionTypes.QualityHold", "احتجاز جودة (QualityHold)") },
            { id: "Quarantine", label: t("restrictionTypes.Quarantine", "حجر صناعي وصحي (Quarantine)") },
            { id: "Blocked", label: t("restrictionTypes.Blocked", "حظر جودة وتوريد (Blocked)") },
            { id: "ReturnsInspection", label: t("restrictionTypes.ReturnsInspection", "فحص واحتجاز مرتجعات (ReturnsInspection)") }
          ]}
        />
        <TextField
          label={t("fields.reasonCode", "كود وسبب الحظر")}
          value={reasonCode}
          onChange={setReasonCode}
          isRequired
          autoFocus
        />
        {isPhysical ? (
          <TextField
            label={t("fields.quantity", "الكمية المحتجزة (مطلوبة للحظر المخزني)")}
            type="number"
            value={quantity}
            onChange={setQuantity}
            placeholder={t("fields.enterHoldQuantity", "أدخل الكمية المحتجزة")}
            isRequired
          />
        ) : (
          <TextField
            label={t("fields.quantity", "الكمية الاسترشادية (اختيارية للحظر الإداري)")}
            type="number"
            value={quantity}
            onChange={setQuantity}
            placeholder={t("fields.optionalQuantity", "اختياري - بدون قفل مخزني")}
          />
        )}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={isSaveDisabled}
          >
            {createRestrictionMutation.isPending ? "..." : t("actions.save", "تطبيق الحظر")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Release Restriction (Partial or Full) Modal
// --------------------------------------------------------------------------

interface ReleaseRestrictionModalProps {
  restrictionId: string;
  totalQuantity?: number;
  remainingQuantity?: number;
  maxQuantity?: number;
  isOpen: boolean;
  onClose: () => void;
}

export function ReleaseRestrictionModal({
  restrictionId,
  remainingQuantity,
  maxQuantity,
  isOpen,
  onClose
}: ReleaseRestrictionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const releaseMutation = useReleaseRestriction();

  const effectiveRemaining = remainingQuantity !== undefined && remainingQuantity !== null
    ? remainingQuantity
    : (maxQuantity ?? 0);

  const [quantity, setQuantity] = useState(effectiveRemaining > 0 ? String(effectiveRemaining) : "");
  const [reason, setReason] = useState("");
  const [operationalDate, setOperationalDate] = useState(new Date().toISOString().split("T")[0]);

  React.useEffect(() => {
    if (effectiveRemaining > 0 && !quantity) {
      setQuantity(String(effectiveRemaining));
    }
  }, [effectiveRemaining, quantity]);

  const numQty = parseFloat(quantity);
  const isOverRemaining = effectiveRemaining > 0 && !isNaN(numQty) && numQty > effectiveRemaining;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!numQty || numQty <= 0 || isOverRemaining || !reason.trim()) return;

    try {
      await releaseMutation.mutateAsync({
        restrictionId,
        request: {
          quantity: numQty,
          reason: reason.trim(),
          operationalDate: operationalDate || null,
          postingDate: operationalDate || null
        }
      });
      onClose();
    } catch (err) {
      console.error("Failed to release restriction:", err);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.releaseRestriction", "إفراج مخزني عن الدفعة المحتجزة")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <div style={{ padding: "8px 12px", backgroundColor: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "6px", fontSize: "13px" }}>
          <div>{t("fields.remainingQuantity", "الكمية المتبقية بالحظر")}: <strong>{effectiveRemaining}</strong></div>
        </div>
        <TextField
          label={t("fields.quantity", "كمية الإفراج (جزئي أو كلي)")}
          type="number"
          value={quantity}
          onChange={setQuantity}
          isRequired
          autoFocus
        />
        {isOverRemaining && (
          <div style={{ color: "#dc2626", fontSize: "0.8125rem", fontWeight: 600 }}>
            {t("messages.releaseQuantityExceeded", "تنبيه: لا يمكن الإفراج عن كمية أكبر من الرصيد المحتجز المتبقي")} ({effectiveRemaining}).
          </div>
        )}
        <TextField
          label={t("fields.reason", "مبرر وأسباب الإفراج الفني")}
          value={reason}
          onChange={setReason}
          isRequired
        />
        <TextField
          label={t("fields.effectiveFrom", "تاريخ العملية")}
          type="date"
          value={operationalDate}
          onChange={setOperationalDate}
          isRequired
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={releaseMutation.isPending || !reason.trim() || !numQty || numQty <= 0 || isOverRemaining}
          >
            {releaseMutation.isPending ? "..." : t("actions.confirm", "تأكيد الإفراج")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Create Disposition Modal
// --------------------------------------------------------------------------

interface CreateDispositionModalProps {
  legalEntityId: string;
  inspectionId?: string | null;
  defaultInspectionId?: string | null;
  defaultLotQuantity?: number | null;
  holdId?: string | null;
  holdRemainingQuantity?: number | null;
  productVariantId?: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateDispositionModal({
  legalEntityId,
  inspectionId,
  defaultInspectionId,
  defaultLotQuantity,
  holdId,
  holdRemainingQuantity,
  productVariantId,
  isOpen,
  onClose
}: CreateDispositionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createDispositionMutation = useCreateDisposition();
  const { data: inspectionsData } = useQualityInspections(legalEntityId);

  const initialInspection = inspectionId || defaultInspectionId || "";
  const [selectedInspectionId, setSelectedInspectionId] = useState(initialInspection);
  const [decision, setDecision] = useState("Accept");
  const [reason, setReason] = useState("");
  const [quantity, setQuantity] = useState(
    holdRemainingQuantity
      ? String(holdRemainingQuantity)
      : (defaultLotQuantity ? String(defaultLotQuantity) : "")
  );
  const [validationError, setValidationError] = useState<string | null>(null);

  React.useEffect(() => {
    if (initialInspection) {
      setSelectedInspectionId(initialInspection);
    }
    if (holdRemainingQuantity && !quantity) {
      setQuantity(String(holdRemainingQuantity));
    } else if (defaultLotQuantity && !quantity) {
      setQuantity(String(defaultLotQuantity));
    }
  }, [initialInspection, defaultLotQuantity, holdRemainingQuantity, quantity]);

  const handleInspectionChange = (inspId: string) => {
    setSelectedInspectionId(inspId);
    const found = inspectionsData?.items?.find((i) => i.id === inspId);
    if (found && !quantity) {
      setQuantity(String(found.lotQuantity));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError(null);
    if (!reason.trim()) return;

    const numQty = quantity ? Number(quantity) : null;
    if (numQty !== null && numQty <= 0) {
      setValidationError(t("validation.positiveQuantity", "يجب أن تكون الكمية أكبر من صفر"));
      return;
    }
    if (holdRemainingQuantity !== null && holdRemainingQuantity !== undefined && numQty !== null && numQty > holdRemainingQuantity) {
      setValidationError(t("validation.quantityExceedsHold", {
        max: holdRemainingQuantity,
        defaultValue: `الكمية المدخلة تتجاوز الرصيد المحتجز المتاح (${holdRemainingQuantity})`
      }));
      return;
    }

    await createDispositionMutation.mutateAsync({
      legalEntityId,
      request: {
        decision,
        reason: reason.trim(),
        quantity: numQty,
        inspectionId: selectedInspectionId || null,
        restrictionId: holdId || null,
        externalReference: holdId ? `HOLD:${holdId}` : null
      }
    });
    onClose();
  };

  const inspectionOptions = [
    { id: "", label: t("fields.noInspectionLinked", "— بدون ربط بأمر فحص (اختياري) —") },
    ...(inspectionsData?.items ?? []).map((ins) => ({
      id: ins.id,
      label: `${ins.number} - ${ins.eventType} (${ins.lotQuantity})`
    }))
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createDisposition", "إصدار قرار فني وتصرف (Disposition)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        {holdId && (
          <div
            data-testid="disposition-hold-source-banner"
            style={{
              padding: "10px 12px",
              backgroundColor: "#eff6ff",
              border: "1px solid #bfdbfe",
              borderRadius: "6px",
              fontSize: "12px",
              color: "#1e40af"
            }}
          >
            <div><strong>{t("fields.sourceHold", "الحظر المصدر")}:</strong> <BdiText>#{holdId.slice(0, 8)}</BdiText></div>
            {holdRemainingQuantity !== null && holdRemainingQuantity !== undefined && (
              <div style={{ marginTop: "2px" }}>
                <strong>{t("fields.heldRemaining", "الرصيد المحتجز المتاح للتصرف")}:</strong> <BdiText>{String(holdRemainingQuantity)}</BdiText>
              </div>
            )}
          </div>
        )}

        {!initialInspection && !holdId && inspectionOptions.length > 1 && (
          <Select
            label={t("navigation.inspections", "أمر الفحص المرتبط بالقرار الفني")}
            selectedKey={selectedInspectionId}
            onSelectionChange={(key) => handleInspectionChange(String(key))}
            options={inspectionOptions}
          />
        )}
        <Select
          label={t("fields.decision", "نوع القرار الفني")}
          selectedKey={decision}
          onSelectionChange={(key) => setDecision(String(key))}
          options={[
            { id: "Accept", label: t("decisions.Accept", "قبول تام وإفراج") },
            { id: "Reject", label: t("decisions.Reject", "رفض وحظر نهائي") },
            { id: "UseAsIs", label: t("decisions.UseAsIs", "استخدام بالحالة الراهنة (Use-As-Is)") },
            { id: "Concession", label: t("decisions.Concession", "سماح وتجاوز فني (Concession)") },
            { id: "SupplierReturn", label: t("decisions.SupplierReturn", "إرجاع للمورد (Supplier Return)") },
            { id: "Scrap", label: t("decisions.Scrap", "تخريد وإتلاف (Scrap)") },
            { id: "Rework", label: t("decisions.Rework", "إعادة تشغيل ومعالجة (Rework)") }
          ]}
        />
        {decision === "SupplierReturn" && (
          <div style={{ padding: "8px 12px", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "6px", fontSize: "12px", color: "#92400e" }}>
            {t("messages.supplierReturnSafetyNotice", "أمان المخزون: إرجاع المورد يُصرَف مباشرة من رصيد احتجاز الجودة (QualityHold) ولا يمر إطلاقاً برصيد المتاح للبيع.")}
          </div>
        )}
        {decision === "Scrap" && (
          <div style={{ padding: "8px 12px", backgroundColor: "#fef2f2", border: "1px solid #fecaca", borderRadius: "6px", fontSize: "12px", color: "#991b1b" }}>
            {t("messages.scrapWarning", "تنبيه مالي وإتلاف: تخريد المخزون ينتج عنه قيد مالي إتلافي وإسقاط نهائي من الأرصدة المستودعية ولا يمكن التراجع عنه.")}
          </div>
        )}
        <TextField
          label={t("fields.quantity", "الكمية الخاضعة للقرار")}
          type="number"
          value={quantity}
          onChange={setQuantity}
          placeholder={t("fields.enterQuantity", "أدخل الكمية")}
        />
        <TextField
          label={t("fields.reason", "المبرر والأسباب الفنية")}
          value={reason}
          onChange={setReason}
          isRequired
          autoFocus
        />
        {validationError && (
          <div role="alert" style={{ color: "#dc2626", fontSize: "0.8125rem", fontWeight: 600 }}>
            {validationError}
          </div>
        )}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createDispositionMutation.isPending || !reason.trim()}
          >
            {createDispositionMutation.isPending ? "..." : t("actions.save", "حفظ القرار كمسودة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Approve Disposition Modal
// --------------------------------------------------------------------------

interface ApproveDispositionModalProps {
  dispositionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function ApproveDispositionModal({ dispositionId, isOpen, onClose }: ApproveDispositionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const approveMutation = useApproveDisposition();
  const [notes, setNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await approveMutation.mutateAsync({
      dispositionId,
      request: { notes: notes.trim() || null }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.approveDisposition", "اعتماد القرار الفني")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <p style={{ fontSize: "13px", color: "#475569", margin: 0 }}>
          {t("messages.irreversibleWarning", "اعتماد القرار يتيح تنفيذه مباشرة عبر الموديول المالك (المخزون / المشتريات / التصنيع).")}
        </p>
        <TextField
          label={t("fields.notes", "ملاحظات الاعتماد")}
          value={notes}
          onChange={setNotes}
          autoFocus
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={approveMutation.isPending}
          >
            {approveMutation.isPending ? "..." : t("actions.confirm", "تأكيد الاعتماد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
