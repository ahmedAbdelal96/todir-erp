import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreatePayrollComponentMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface NewPayrollComponentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewPayrollComponentModal({
  isOpen,
  onClose,
  legalEntityId
}: NewPayrollComponentModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreatePayrollComponentMutation(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [category, setCategory] = useState("Earning");
  const [direction, setDirection] = useState("Credit");
  const [inputMode, setInputMode] = useState("Manual");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCode("");
    setName("");
    setCategory("Earning");
    setDirection("Credit");
    setInputMode("Manual");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) {
      setErrorMessage(t("setup.components.codeRequired", "رمز البند مطلوب"));
      return;
    }
    if (!name.trim()) {
      setErrorMessage(t("setup.components.nameRequired", "اسم البند مطلوب"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        code: code.trim().toUpperCase(),
        name: name.trim(),
        category,
        direction,
        inputMode
      });
      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const categoryOptions = [
    { id: "Earning", label: t("setup.componentCategories.earning", "استحقاق (Earning)") },
    { id: "EmployeeDeduction", label: t("setup.componentCategories.employeeDeduction", "استقطاع موظف (Deduction)") },
    { id: "EmployerContribution", label: t("setup.componentCategories.employerContribution", "مساهمة منشأة (Employer Contribution)") },
    { id: "Informational", label: t("setup.componentCategories.informational", "معلوماتي (Informational)") },
    { id: "Recovery", label: t("setup.componentCategories.recovery", "استرداد سلفة (Recovery)") }
  ];

  const directionOptions = [
    { id: "Credit", label: t("setup.componentDirections.credit", "إضافة للراتب (Credit)") },
    { id: "Debit", label: t("setup.componentDirections.debit", "خصم من الراتب (Debit)") },
    { id: "Memo", label: t("setup.componentDirections.memo", "قيد بياني (Memo)") }
  ];

  const inputModeOptions = [
    { id: "Manual", label: t("setup.componentInputModes.manual", "يدوي (Manual)") },
    { id: "Formula", label: t("setup.componentInputModes.formula", "معادلة / آلي (Formula)") },
    { id: "Import", label: t("setup.componentInputModes.import", "استيراد ملف (Import)") },
    { id: "Provider", label: t("setup.componentInputModes.provider", "مزود قانوني (Provider)") },
    { id: "TimeAndAttendance", label: t("setup.componentInputModes.timeAndAttendance", "حضور وانصراف (Time & Attendance)") }
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
      title={t("setup.components.newComponentModalTitle", "إضافة بند راتب جديد")}
      description={t("setup.components.newComponentModalDesc", "تعريف رمز البند، الاتجاه المحاسبي وطريقة الاحتساب")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={createMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-component-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <form
        id="new-component-form"
        onSubmit={handleSubmit}
        style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
      >
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("setup.componentsCode", "رمز البند")}
            value={code}
            onChange={setCode}
            placeholder={t("setup.components.codePlaceholder", "مثال: HOUSING, BONUS")}
            isRequired
          />

          <TextField
            label={t("setup.componentsName", "اسم البند")}
            value={name}
            onChange={setName}
            placeholder={t("setup.components.namePlaceholder", "مثال: بدل سكن، مكافأة أداء")}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("setup.componentsCategory", "تصنيف البند")}
            selectedKey={category}
            onSelectionChange={(val) => {
              if (val) {
                const cat = String(val);
                setCategory(cat);
                if (cat === "Earning") setDirection("Credit");
                else if (cat === "EmployeeDeduction" || cat === "Recovery") setDirection("Debit");
              }
            }}
            options={categoryOptions}
          />

          <Select
            label={t("setup.componentsDirection", "الاتجاه")}
            selectedKey={direction}
            onSelectionChange={(val) => {
              if (val) setDirection(String(val));
            }}
            options={directionOptions}
          />
        </div>

        <Select
          label={t("setup.componentsInputMode", "طريقة الاحتساب والمدخلات")}
          selectedKey={inputMode}
          onSelectionChange={(val) => {
            if (val) setInputMode(String(val));
          }}
          options={inputModeOptions}
        />
      </form>
    </Dialog>
  );
}
