import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { useCreateCustomFieldMutation } from "../../hooks/use-crm-queries.ts";

interface NewCustomFieldModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
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
  const [fieldType, setFieldType] = useState<string>("Text");
  const [isRequired, setIsRequired] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateCustomFieldMutation(legalEntityId);

  const reset = () => {
    setTargetType("Lead");
    setFieldName("");
    setFieldLabel("");
    setFieldType("Text");
    setIsRequired(false);
    setErrorMessage(null);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!fieldName.trim() || !fieldLabel.trim()) {
      setErrorMessage(t("crm.validation.fieldsRequired", "يرجى تعبئة الحقول الإلزامية"));
      return;
    }

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
          reset();
          onClose();
        },
        onError: (err: any) => {
          setErrorMessage(err?.response?.data?.detail || err?.message || t("crm.common.saveFailed", "فشل الحفظ"));
        },
      }
    );
  };

  const targetOptions = [
    { id: "Lead", label: t("crm.customFields.targetLead", "عميل محتمل (Lead)") },
    { id: "Opportunity", label: t("crm.customFields.targetOpportunity", "فرصة بيعية (Opportunity)") },
  ];

  const typeOptions = [
    { id: "Text", label: t("crm.customFields.typeText", "نص عادي (Text)") },
    { id: "Integer", label: t("crm.customFields.typeNumber", "رقمي (Integer)") },
    { id: "Decimal", label: t("crm.customFields.typeDecimal", "رقم عشري (Decimal)") },
    { id: "Date", label: t("crm.customFields.typeDate", "تاريخ (Date)") },
    { id: "Boolean", label: t("crm.customFields.typeBoolean", "نعم / لا (Boolean)") },
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("crm.customFields.newTitle", "تعريف حقل مخصص جديد")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
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
      <form id="new-custom-field-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.875rem" }}>
          <Select
            label={t("crm.customFields.targetEntity", "الجهة المستهدفة")}
            selectedKey={targetType}
            onSelectionChange={(val) => {
              if (val) setTargetType(String(val) as "Lead" | "Opportunity");
            }}
            options={targetOptions}
          />
          <Select
            label={t("crm.customFields.fieldType", "نوع الحقل")}
            selectedKey={fieldType}
            onSelectionChange={(val) => {
              if (val) setFieldType(String(val));
            }}
            options={typeOptions}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.875rem" }}>
          <TextField
            label={t("crm.customFields.fieldName", "اسم الحقل البرمجي (Field Key)")}
            value={fieldName}
            onChange={setFieldName}
            placeholder={t("crm.customFields.fieldNamePlaceholder", "decision_role")}
            isRequired
          />
          <TextField
            label={t("crm.customFields.fieldLabel", "تسمية الحقل الظاهرة (Label)")}
            value={fieldLabel}
            onChange={setFieldLabel}
            placeholder={t("crm.customFields.labelPlaceholder", "مثال: حجم أسطول السيارات")}
            isRequired
          />
        </div>

        <div style={{ marginTop: "0.25rem" }}>
          <Checkbox
            isSelected={isRequired}
            onChange={setIsRequired}
          >
            {t("crm.customFields.isRequired", "إلزامي")}
          </Checkbox>
        </div>
      </form>
    </Dialog>
  );
};
