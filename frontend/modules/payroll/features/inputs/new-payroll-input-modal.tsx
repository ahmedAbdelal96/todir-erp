import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrEmployeePicker } from "../../../hr/public.ts";
import { useCreatePayrollInputMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface NewPayrollInputModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewPayrollInputModal({
  isOpen,
  onClose,
  legalEntityId
}: NewPayrollInputModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreatePayrollInputMutation(legalEntityId);

  const [employmentId, setEmploymentId] = useState<string>("");
  const [componentCode, setComponentCode] = useState<string>("BONUS");
  const [componentType, setComponentType] = useState<string>("Earning");
  const [inputDate, setInputDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<number>(0);
  const [currencyCode, setCurrencyCode] = useState<string>("EGP");
  const [notes, setNotes] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setEmploymentId("");
    setComponentCode("BONUS");
    setComponentType("Earning");
    setInputDate(new Date().toISOString().slice(0, 10));
    setAmount(0);
    setCurrencyCode("EGP");
    setNotes("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!employmentId) {
      setErrorMessage(t("inputs.employeeRequired"));
      return;
    }
    if (amount <= 0) {
      setErrorMessage(t("inputs.amountPositiveRequired"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        employmentId,
        componentId: componentCode.trim().toUpperCase(),
        inputDate,
        amount,
        currencyCode,
        source: "Manual",
        notes: notes.trim() || null
      });

      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const componentTypeOptions = [
    { id: "Earning", label: t("components.types.earning") },
    { id: "Deduction", label: t("components.types.deduction") }
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
      title={t("inputs.newInputModalTitle")}
      description={t("inputs.newInputModalDesc")}
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
            form="new-payroll-input-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("inputs.saveInput")}
          </Button>
        </>
      }
    >
      <form
        id="new-payroll-input-form"
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

        <HrEmployeePicker
          label={t("inputs.targetEmployee")}
          value={employmentId}
          onChange={setEmploymentId}
          legalEntityId={legalEntityId}
          isRequired
          placeholder={t("inputs.selectEmployeePlaceholder")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("inputs.componentType")}
            selectedKey={componentType}
            onSelectionChange={(val) => {
              if (val) setComponentType(String(val));
            }}
            options={componentTypeOptions}
          />

          <TextField
            label={t("inputs.componentCode")}
            value={componentCode}
            onChange={setComponentCode}
            isRequired
            placeholder={t("inputs.componentCodePlaceholder")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("inputs.amount")}
            value={String(amount || "")}
            onChange={(val) => setAmount(parseFloat(val) || 0)}
            isRequired
            type="number"
          />

          <TextField
            label={t("common.currency")}
            value={currencyCode}
            onChange={setCurrencyCode}
            isRequired
            isDisabled
          />
        </div>

        <LocalDateField
          label={t("inputs.inputDate")}
          value={inputDate}
          onChange={setInputDate}
          isRequired
        />

        <TextField
          label={t("inputs.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("inputs.notesPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
