import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateCustomFieldMutation } from "../../hooks/use-crm-queries.ts";
import { Sliders, AlertCircle } from "../../components/crm-icons.tsx";

interface NewCustomFieldModalProps {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId: string;
}

export const NewCustomFieldModal: React.FC<NewCustomFieldModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
}) => {
  const { t } = useTranslation("crm");
  const [targetType, setTargetType] = useState<"Lead" | "Opportunity">("Lead");
  const [fieldName, setFieldName] = useState("");
  const [fieldLabel, setFieldLabel] = useState("");
  const [fieldType, setFieldType] = useState<"text" | "number" | "select" | "boolean" | "date">("text");
  const [isRequired, setIsRequired] = useState(false);
  const [optionsText, setOptionsText] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateCustomFieldMutation(legalEntityId);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!fieldName.trim() || !fieldLabel.trim()) {
      setErrorMessage(t("crm.validation.fieldsRequired", "يرجى تعبئة الحقول الإلزامية"));
      return;
    }

    const options = fieldType === "select"
      ? optionsText.split(",").map((s) => s.trim()).filter(Boolean)
      : undefined;

    createMutation.mutate(
      {
        legalEntityId,
        entityType: targetType,
        fieldKey: fieldName.trim().toLowerCase().replace(/\s+/g, "_"),
        label: fieldLabel.trim(),
        dataType: fieldType,
      },
      {
        onSuccess: () => {
          onClose();
        },
        onError: (err: any) => {
          setErrorMessage(err?.response?.data?.detail || err?.message || t("crm.common.saveFailed", "فشل الحفظ"));
        },
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => !open && onClose()}
      title={t("crm.customFields.newTitle", "تعريف حقل مخصص جديد")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={createMutation.isPending}>
            {t("crm.common.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-custom-field-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.common.save", "حفظ")}
          </Button>
        </>
      }
    >
      <form id="new-custom-field-form" onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && (
          <div className="p-3 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.customFields.targetEntity", "الجهة المستهدفة")}
          </label>
          <select
            value={targetType}
            onChange={(e) => setTargetType(e.target.value as "Lead" | "Opportunity")}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          >
            <option value="Lead">{t("crm.customFields.targetLead", "عميل محتمل (Lead)")}</option>
            <option value="Opportunity">{t("crm.customFields.targetOpportunity", "فرصة بيعية (Opportunity)")}</option>
          </select>
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.customFields.fieldName", "اسم الحقل البرمجي (Field Key)")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.customFields.fieldNamePlaceholder", "decision_role")}
            value={fieldName}
            onChange={(e) => setFieldName(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500 font-mono text-xs"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.customFields.fieldLabel", "تسمية الحقل الظاهرة (Label)")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.customFields.labelPlaceholder", "مثال: حجم أسطول السيارات")}
            value={fieldLabel}
            onChange={(e) => setFieldLabel(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.customFields.fieldType", "نوع الحقل")}
          </label>
          <select
            value={fieldType}
            onChange={(e) => setFieldType(e.target.value as any)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          >
            <option value="text">{t("crm.customFields.typeText", "نص عادي (Text)")}</option>
            <option value="number">{t("crm.customFields.typeNumber", "رقم (Number)")}</option>
            <option value="select">{t("crm.customFields.typeSelect", "قائمة خيارات (Dropdown Select)")}</option>
            <option value="boolean">{t("crm.customFields.typeBoolean", "نعم / لا (Boolean)")}</option>
            <option value="date">{t("crm.customFields.typeDate", "تاريخ (Date)")}</option>
          </select>
        </div>

        {fieldType === "select" && (
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              {t("crm.customFields.selectOptions", "الخيارات المتاحة (مفصولة بفاصلة)")}
            </label>
            <input
              type="text"
              placeholder={t("crm.customFields.selectOptionsPlaceholder", "خيار 1, خيار 2, خيار 3")}
              value={optionsText}
              onChange={(e) => setOptionsText(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
        )}

        <div className="flex items-center gap-2 pt-1">
          <input
            type="checkbox"
            id="isRequiredFieldCheck"
            checked={isRequired}
            onChange={(e) => setIsRequired(e.target.checked)}
            className="h-4 w-4 text-primary-600 rounded border-slate-300"
          />
          <label htmlFor="isRequiredFieldCheck" className="text-xs font-medium text-slate-700 cursor-pointer">
            {t("crm.customFields.isRequired", "حقل إلزامي عند الإدخال")}
          </label>
        </div>
      </form>
    </Dialog>
  );
};
