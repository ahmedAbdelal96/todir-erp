import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import {
  useCreateQualityPlan,
  useCreateQualityPlanVersion,
  useCreateCharacteristic,
  useCreateSamplingPlan,
  useAddPlanCharacteristic
} from "../hooks/use-quality-queries.ts";

// --------------------------------------------------------------------------
// 1. Create Quality Plan Modal
// --------------------------------------------------------------------------

interface CreatePlanModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreatePlanModal({ legalEntityId, isOpen, onClose }: CreatePlanModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createPlanMutation = useCreateQualityPlan();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [eventType, setEventType] = useState("Receiving");
  const [mandatory, setMandatory] = useState(true);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    await createPlanMutation.mutateAsync({
      legalEntityId,
      code: code.trim(),
      name: name.trim(),
      eventType,
      mandatory
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createPlan", "إنشاء خطة جودة جديدة")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <TextField
          label={t("fields.code", "الكود التعريفي")}
          value={code}
          onChange={setCode}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.name", "الاسم والبيان")}
          value={name}
          onChange={setName}
          isRequired
        />
        <Select
          label={t("fields.eventType", "نوع الحدث ومرحلة الفحص")}
          selectedKey={eventType}
          onSelectionChange={(key) => setEventType(String(key))}
          options={[
            { id: "Receiving", label: t("eventTypes.Receiving", "فحص استلام مشتريات") },
            { id: "InProcess", label: t("eventTypes.InProcess", "فحص مرحلي أثناء التشغيل") },
            { id: "Final", label: t("eventTypes.Final", "فحص نهائي للمنتج التام") },
            { id: "Inventory", label: t("eventTypes.Inventory", "فحص دوري / مخزني") },
            { id: "Outgoing", label: t("eventTypes.Outgoing", "فحص تسليم خارجي") }
          ]}
        />
        <label style={{ display: "flex", alignItems: "center", gap: "8px", fontSize: "14px", color: "#334155", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={mandatory}
            onChange={(e) => setMandatory(e.target.checked)}
          />
          {t("fields.mandatory", "إلزامي لمنع التداول قبل الفحص")}
        </label>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createPlanMutation.isPending || !code.trim() || !name.trim()}
          >
            {createPlanMutation.isPending ? "..." : t("actions.save", "حفظ البيانات")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Create Plan Version Modal
// --------------------------------------------------------------------------

interface CreateVersionModalProps {
  planId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateVersionModal({ planId, isOpen, onClose }: CreateVersionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createVersionMutation = useCreateQualityPlanVersion();

  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().split("T")[0]);
  const [effectiveTo, setEffectiveTo] = useState("");
  const [notes, setNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveFrom) return;

    await createVersionMutation.mutateAsync({
      planId,
      request: {
        effectiveFrom,
        effectiveTo: effectiveTo || null,
        notes: notes.trim() || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createVersion", "إصدار نسخة جديدة للخطة")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <TextField
          label={t("fields.effectiveFrom", "ساري اعتباراً من")}
          type="date"
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />
        <TextField
          label={t("fields.effectiveTo", "تاريخ الانتهاء (اختياري)")}
          type="date"
          value={effectiveTo}
          onChange={setEffectiveTo}
        />
        <TextField
          label={t("fields.notes", "ملاحظات وتوجيهات")}
          value={notes}
          onChange={setNotes}
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createVersionMutation.isPending || !effectiveFrom}
          >
            {createVersionMutation.isPending ? "..." : t("actions.save", "حفظ النسخة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Create Characteristic Modal
// --------------------------------------------------------------------------

interface CreateCharacteristicModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateCharacteristicModal({ legalEntityId, isOpen, onClose }: CreateCharacteristicModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createCharMutation = useCreateCharacteristic();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [dataType, setDataType] = useState("Numeric");
  const [baseUomCode, setBaseUomCode] = useState("");
  const [required, setRequired] = useState(true);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    await createCharMutation.mutateAsync({
      legalEntityId,
      code: code.trim(),
      name: name.trim(),
      dataType,
      baseUomCode: baseUomCode.trim() || null,
      required
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createCharacteristic", "إضافة خاصية فحص جديدة")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <TextField
          label={t("fields.code", "الكود")}
          value={code}
          onChange={setCode}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.name", "الاسم")}
          value={name}
          onChange={setName}
          isRequired
        />
        <Select
          label={t("fields.dataType", "نوع البيانات")}
          selectedKey={dataType}
          onSelectionChange={(key) => setDataType(String(key))}
          options={[
            { id: "Numeric", label: t("dataTypes.Numeric", "قيمة رقمية (قياس)") },
            { id: "Text", label: t("dataTypes.Text", "نص وصفي") },
            { id: "Boolean", label: t("dataTypes.Boolean", "مطابق / غير مطابق (Pass/Fail)") }
          ]}
        />
        {dataType === "Numeric" && (
          <TextField
            label={t("fields.uom", "وحدة القياس")}
            value={baseUomCode}
            onChange={setBaseUomCode}
          />
        )}
        <label style={{ display: "flex", alignItems: "center", gap: "8px", fontSize: "14px", color: "#334155", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={required}
            onChange={(e) => setRequired(e.target.checked)}
          />
          {t("fields.required", "خاصية فحص إلزامية")}
        </label>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createCharMutation.isPending || !code.trim() || !name.trim()}
          >
            {createCharMutation.isPending ? "..." : t("actions.save", "حفظ الخاصية")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Create Sampling Plan Modal
// --------------------------------------------------------------------------

interface CreateSamplingPlanModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateSamplingPlanModal({ legalEntityId, isOpen, onClose }: CreateSamplingPlanModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createSamplingMutation = useCreateSamplingPlan();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [method, setMethod] = useState("Percentage");
  const [fixedQuantity, setFixedQuantity] = useState<string>("5");
  const [percentage, setPercentage] = useState<string>("10");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    await createSamplingMutation.mutateAsync({
      legalEntityId,
      code: code.trim(),
      name: name.trim(),
      method,
      fixedQuantity: method === "Fixed" ? Number(fixedQuantity) : null,
      percentage: method === "Percentage" ? Number(percentage) : null
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createSamplingPlan", "إنشاء خطة سحب عينات")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <TextField
          label={t("fields.code", "الكود")}
          value={code}
          onChange={setCode}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.name", "الاسم")}
          value={name}
          onChange={setName}
          isRequired
        />
        <Select
          label={t("fields.method", "طريقة سحب العينة")}
          selectedKey={method}
          onSelectionChange={(key) => setMethod(String(key))}
          options={[
            { id: "Percentage", label: t("samplingMethods.Percentage", "نسبة مئوية من التشغيلة") },
            { id: "Fixed", label: t("samplingMethods.Fixed", "عينة بعدد ثابت") },
            { id: "Full", label: t("samplingMethods.Full", "فحص شامل (100٪)") }
          ]}
        />
        {method === "Percentage" && (
          <TextField
            label={t("fields.percentage", "النسبة المئوية (٪)")}
            type="number"
            value={percentage}
            onChange={setPercentage}
            isRequired
          />
        )}
        {method === "Fixed" && (
          <TextField
            label={t("fields.fixedQty", "الكمية الثابتة للعينة")}
            type="number"
            value={fixedQuantity}
            onChange={setFixedQuantity}
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
            isDisabled={createSamplingMutation.isPending || !code.trim() || !name.trim()}
          >
            {createSamplingMutation.isPending ? "..." : t("actions.save", "حفظ خطة العينات")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
