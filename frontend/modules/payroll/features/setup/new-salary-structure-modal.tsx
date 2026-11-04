import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateSalaryStructureMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface NewSalaryStructureModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewSalaryStructureModal({
  isOpen,
  onClose,
  legalEntityId
}: NewSalaryStructureModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreateSalaryStructureMutation(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCode("");
    setName("");
    setCurrencyCode("EGP");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) {
      setErrorMessage(t("setup.structures.codeRequired", "رمز الهيكل مطلوب"));
      return;
    }
    if (!name.trim()) {
      setErrorMessage(t("setup.structures.nameRequired", "اسم هيكل الرواتب مطلوب"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        currencyCode: currencyCode.trim().toUpperCase()
      });
      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("setup.structures.newStructureModalTitle", "إضافة هيكل رواتب جديد")}
      description={t("setup.structures.newStructureModalDesc", "تعريف هيكل أجور لربط الموظفين ببنود الاستحقاق والاستقطاع")}
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
            form="new-structure-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <form
        id="new-structure-form"
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
            label={t("setup.structuresCode", "رمز الهيكل")}
            value={code}
            onChange={setCode}
            placeholder={t("setup.structures.codePlaceholder", "مثال: EXEC-GRADE, SALES-REP")}
            isRequired
          />

          <TextField
            label={t("common.currency", "العملة")}
            value={currencyCode}
            onChange={setCurrencyCode}
            placeholder={t("setup.structures.currencyPlaceholder", "EGP")}
            isRequired
          />
        </div>

        <TextField
          label={t("setup.structuresName", "اسم هيكل الرواتب")}
          value={name}
          onChange={setName}
          placeholder={t("setup.structures.namePlaceholder", "مثال: هيكل رواتب الإدارة التنفيذية")}
          isRequired
        />
      </form>
    </Dialog>
  );
}
