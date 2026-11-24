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
  useQualityInspections
} from "../hooks/use-quality-queries.ts";

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
  const { data: inspectionsData } = useQualityInspections(legalEntityId);

  const initialInspection = inspectionId || defaultInspectionId || "";
  const [selectedInspectionId, setSelectedInspectionId] = useState(initialInspection);
  const [restrictionType, setRestrictionType] = useState("QualityHold");
  const [reasonCode, setReasonCode] = useState("RECEIVING_INSPECTION_HOLD");
  const [quantity, setQuantity] = useState(defaultQuantity ? String(defaultQuantity) : "");
  const [customProductVariantId, setCustomProductVariantId] = useState("");

  // Update inspection when initial prop or inspectionsData load
  React.useEffect(() => {
    if (initialInspection) {
      setSelectedInspectionId(initialInspection);
    } else if (inspectionsData?.items && inspectionsData.items.length > 0 && !selectedInspectionId) {
      // Prioritize canonical seeded receiving inspection or first inspection with authoritative product variant linkage
      const preferred = inspectionsData.items.find((i) => i.number === "QI-SEED-0001") ||
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
    if (found && !quantity) {
      setQuantity(String(found.lotQuantity));
    }
  };

  const matchedInspection = inspectionsData?.items?.find((i) => i.id === selectedInspectionId.trim());

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const finalInspectionId = selectedInspectionId.trim();
    const qty = Number(quantity);
    if (!finalInspectionId || !qty || qty <= 0 || !reasonCode.trim()) return;

    const resolvedProductVariantId = matchedInspection?.productVariantId || customProductVariantId.trim() || null;
    try {
      await createRestrictionMutation.mutateAsync({
        inspectionId: finalInspectionId,
        legalEntityId,
        request: {
          restrictionType,
          reasonCode: reasonCode.trim(),
          quantity: qty,
          inspectionId: finalInspectionId,
          productVariantId: resolvedProductVariantId
        }
      });
      onClose();
    } catch (err) {
      console.error("Failed to create restriction:", err);
    }
  };

  const inspectionOptions = (inspectionsData?.items ?? []).map((ins) => ({
    id: ins.id,
    label: `${ins.number} - ${ins.eventType} (${ins.lotQuantity})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createRestriction", "حظر واحتجاز الدفعة (Quality Hold)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        {!initialInspection && inspectionOptions.length > 0 && (
          <Select
            label={t("navigation.inspections", "أمر الفحص المرتبط بالاحتجاز")}
            selectedKey={selectedInspectionId}
            onSelectionChange={(key) => handleInspectionChange(String(key))}
            options={inspectionOptions}
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
        <TextField
          label={t("fields.quantity", "الكمية المحتجزة")}
          type="number"
          value={quantity}
          onChange={setQuantity}
          placeholder={t("fields.enterHoldQuantity", "أدخل الكمية المحتجزة")}
          isRequired
        />
        {matchedInspection && !matchedInspection.productVariantId && (
          <TextField
            label={t("fields.productVariantId", "معرف صنف المنتج (مطلوب لربط حظر المخزون)")}
            value={customProductVariantId}
            onChange={setCustomProductVariantId}
            placeholder={t("fields.enterProductVariantId", "أدخل معرف صنف المنتج")}
            isRequired
          />
        )}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={
              createRestrictionMutation.isPending ||
              !reasonCode.trim() ||
              !quantity ||
              Number(quantity) <= 0 ||
              !selectedInspectionId ||
              Boolean(matchedInspection && !matchedInspection.productVariantId && !customProductVariantId.trim())
            }
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
