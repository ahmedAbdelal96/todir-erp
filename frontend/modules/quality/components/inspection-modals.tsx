import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import {
  useCreateInspection,
  useAddInspectionResult,
  useCompleteInspection,
  useReinspect,
  useQualityCharacteristics
} from "../hooks/use-quality-queries.ts";

// --------------------------------------------------------------------------
// 1. Create Inspection Modal
// --------------------------------------------------------------------------

interface CreateInspectionModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateInspectionModal({ legalEntityId, isOpen, onClose }: CreateInspectionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createInspectionMutation = useCreateInspection();

  const [eventType, setEventType] = useState("Receiving");
  const [lotQuantity, setLotQuantity] = useState("100");
  const [lotReference, setLotReference] = useState("");
  const [sourceDocumentId, setSourceDocumentId] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const qty = Number(lotQuantity);
    if (!qty || qty <= 0) return;

    await createInspectionMutation.mutateAsync({
      legalEntityId,
      eventType,
      lotQuantity: qty,
      lotReference: lotReference.trim() || null,
      sourceDocumentId: sourceDocumentId.trim() || null
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createInspection", "إنشاء أمر فحص جديد")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <Select
          label={t("fields.eventType", "مرحلة ونوع الفحص")}
          selectedKey={eventType}
          onSelectionChange={(key) => setEventType(String(key))}
          options={[
            { id: "Receiving", label: t("eventTypes.Receiving", "فحص استلام مشتريات") },
            { id: "InProcess", label: t("eventTypes.InProcess", "فحص مرحلي أثناء التشغيل") },
            { id: "Final", label: t("eventTypes.Final", "فحص نهائي للمنتج التام") },
            { id: "Inventory", label: t("eventTypes.Inventory", "فحص دوري / مخزني") }
          ]}
        />
        <TextField
          label={t("fields.lotQuantity", "حجم التشغيلة / الدفعة")}
          type="number"
          value={lotQuantity}
          onChange={setLotQuantity}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.lotReference", "رقم التشغيلة / اللوت")}
          value={lotReference}
          onChange={setLotReference}
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createInspectionMutation.isPending || !lotQuantity || Number(lotQuantity) <= 0}
          >
            {createInspectionMutation.isPending ? "..." : t("actions.save", "إنشاء أمر الفحص")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Record Inspection Result Modal
// --------------------------------------------------------------------------

interface RecordResultModalProps {
  inspectionId: string;
  legalEntityId?: string;
  characteristicId?: string;
  characteristicName?: string;
  dataType?: string;
  uomCode?: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function RecordResultModal({
  inspectionId,
  legalEntityId,
  characteristicId: initialCharId,
  characteristicName: initialCharName,
  dataType: initialDataType = "Numeric",
  uomCode: initialUom,
  isOpen,
  onClose
}: RecordResultModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const addResultMutation = useAddInspectionResult();
  const { data: charsData } = useQualityCharacteristics(legalEntityId ?? "");

  const [activeCharId, setActiveCharId] = useState(initialCharId || "");
  const [activeDataType, setActiveDataType] = useState(initialDataType);
  const [activeUom, setActiveUom] = useState(initialUom);
  const [activeName, setActiveName] = useState(initialCharName || "");

  const [numericValue, setNumericValue] = useState("");
  const [textValue, setTextValue] = useState("");
  const [booleanValue, setBooleanValue] = useState<string>("true");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [instrumentReference, setInstrumentReference] = useState("");

  // Sync state if initial props change
  React.useEffect(() => {
    if (initialCharId) {
      setActiveCharId(initialCharId);
      setActiveDataType(initialDataType);
      setActiveUom(initialUom);
      setActiveName(initialCharName || "");
    } else if (charsData?.items && charsData.items.length > 0 && !activeCharId) {
      const first = charsData.items[0];
      if (first) {
        setActiveCharId(first.id);
        setActiveDataType(first.dataType || "Numeric");
        setActiveUom(first.baseUomCode || null);
        setActiveName(first.name);
      }
    }
  }, [initialCharId, initialDataType, initialUom, initialCharName, charsData]);

  const handleCharSelect = (charId: string) => {
    setActiveCharId(charId);
    const found = charsData?.items?.find((c) => c.id === charId);
    if (found) {
      setActiveDataType(found.dataType || "Numeric");
      setActiveUom(found.baseUomCode || null);
      setActiveName(found.name);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeCharId) return;

    await addResultMutation.mutateAsync({
      inspectionId,
      request: {
        characteristicId: activeCharId,
        numericValue: activeDataType === "Numeric" && numericValue !== "" ? Number(numericValue) : null,
        textValue: activeDataType === "Text" ? textValue.trim() : null,
        booleanValue: activeDataType === "Boolean" ? booleanValue === "true" : null,
        uomCode: activeUom || null,
        evidenceReference: evidenceReference.trim() || null,
        instrumentReference: instrumentReference.trim() || null,
        calibrationStatus: instrumentReference.trim() ? "Valid" : null
      }
    });
    setNumericValue("");
    setTextValue("");
    setEvidenceReference("");
    setInstrumentReference("");
    onClose();
  };

  const charOptions = (charsData?.items ?? []).map((c) => ({
    id: c.id,
    label: `${c.code} - ${c.name} (${c.dataType})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={activeName ? `${t("actions.enterResults", "تسجيل نتائج القياس")}: ${activeName}` : t("actions.enterResults", "تسجيل نتائج القياس")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        {!initialCharId && charOptions.length > 0 && (
          <Select
            label={t("fields.name", "خاصية الفحص")}
            selectedKey={activeCharId}
            onSelectionChange={(key) => handleCharSelect(String(key))}
            options={charOptions}
          />
        )}
        {activeDataType === "Numeric" && (
          <TextField
            label={activeUom ? `${t("fields.numericValue", "القيمة المقاسة فعلياً")} (${activeUom})` : t("fields.numericValue", "القيمة المقاسة فعلياً")}
            type="number"
            value={numericValue}
            onChange={setNumericValue}
            isRequired
            autoFocus
          />
        )}
        {activeDataType === "Text" && (
          <TextField
            label={t("fields.textValue", "البيان المرصود")}
            value={textValue}
            onChange={setTextValue}
            isRequired
            autoFocus
          />
        )}
        {activeDataType === "Boolean" && (
          <Select
            label={t("fields.booleanValue", "حالة المطابقة")}
            selectedKey={booleanValue}
            onSelectionChange={(key) => setBooleanValue(String(key))}
            options={[
              { id: "true", label: t("status.Passed", "مطابق ومقبول (Pass)") },
              { id: "false", label: t("status.Failed", "غير مطابق (Fail)") }
            ]}
          />
        )}
        <TextField
          label={t("fields.evidence", "مرجع الإثبات / صورة / مستند")}
          value={evidenceReference}
          onChange={setEvidenceReference}
        />
        <TextField
          label={t("fields.instrumentRef", "جهاز القياس المستخدم")}
          value={instrumentReference}
          onChange={setInstrumentReference}
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={addResultMutation.isPending || !activeCharId}
          >
            {addResultMutation.isPending ? "..." : t("actions.save", "تسجيل النتيجة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Complete Inspection Modal
// --------------------------------------------------------------------------

interface CompleteInspectionModalProps {
  inspectionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CompleteInspectionModal({ inspectionId, isOpen, onClose }: CompleteInspectionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const completeMutation = useCompleteInspection();
  const [reviewNotes, setReviewNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await completeMutation.mutateAsync({
      inspectionId,
      request: { reviewNotes: reviewNotes.trim() || null }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.completeInspection", "إتمام واعتماد أمر الفحص")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <div
          style={{
            padding: "10px 14px",
            backgroundColor: "#eff6ff",
            border: "1px solid #bfdbfe",
            borderRadius: "6px",
            fontSize: "13px",
            color: "#1e40af",
            lineHeight: 1.5
          }}
        >
          {t("messages.inspectionPassedNotice", "تنبيه: نجاح الفحص لا يفرج تلقائياً عن المخزون المحتجز. يجب تنفيذ إجراء الإفراج أو اعتماد القرار الفني بعد اكتمال التقييم.")}
        </div>
        <TextField
          label={t("fields.notes", "ملاحظات الفاحص والاعتماد")}
          value={reviewNotes}
          onChange={setReviewNotes}
          autoFocus
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={completeMutation.isPending}
          >
            {completeMutation.isPending ? "..." : t("actions.confirm", "تأكيد واعتماد الإتمام")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Reinspect Modal
// --------------------------------------------------------------------------

interface ReinspectModalProps {
  inspectionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function ReinspectModal({ inspectionId, isOpen, onClose }: ReinspectModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const reinspectMutation = useReinspect();
  const [reason, setReason] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) return;

    await reinspectMutation.mutateAsync({
      inspectionId,
      request: { reason: reason.trim() }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.reinspect", "طلب إعادة فحص (Reinspection)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <TextField
          label={t("fields.reason", "المبرر والأسباب الفنية لإعادة الفحص")}
          value={reason}
          onChange={setReason}
          isRequired
          autoFocus
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={reinspectMutation.isPending || !reason.trim()}
          >
            {reinspectMutation.isPending ? "..." : t("actions.confirm", "تأكيد أمر إعادة الفحص")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
