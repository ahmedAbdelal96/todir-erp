import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrEmployeePicker } from "../../../hr/public.ts";
import { useCreateReceivableMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { ReceivableType } from "../../types.ts";

export interface NewLoanModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewLoanModal({
  isOpen,
  onClose,
  legalEntityId
}: NewLoanModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreateReceivableMutation(legalEntityId);

  const [employmentId, setEmploymentId] = useState<string>("");
  const [receivableType, setReceivableType] = useState<ReceivableType>("Loan");
  const [principalAmount, setPrincipalAmount] = useState<number>(5000);
  const [currency, setCurrency] = useState<string>("EGP");
  const [installmentCount, setInstallmentCount] = useState<number>(5);
  const [installmentAmount, setInstallmentAmount] = useState<number>(1000);
  const [notes, setNotes] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setEmploymentId("");
    setReceivableType("Loan");
    setPrincipalAmount(5000);
    setCurrency("EGP");
    setInstallmentCount(5);
    setInstallmentAmount(1000);
    setNotes("");
    setErrorMessage(null);
  };

  const handlePrincipalChange = (val: number) => {
    setPrincipalAmount(val);
    if (installmentCount > 0) {
      setInstallmentAmount(Math.round((val / installmentCount) * 100) / 100);
    }
  };

  const handleCountChange = (count: number) => {
    setInstallmentCount(count);
    if (count > 0 && principalAmount > 0) {
      setInstallmentAmount(Math.round((principalAmount / count) * 100) / 100);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!employmentId) {
      setErrorMessage(t("loans.employeeRequired"));
      return;
    }
    if (principalAmount <= 0) {
      setErrorMessage(t("loans.principalPositiveRequired"));
      return;
    }
    if (installmentCount <= 0 || installmentAmount <= 0) {
      setErrorMessage(t("loans.installmentPositiveRequired"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        employmentId,
        type: receivableType,
        principal: principalAmount,
        currencyCode: currency,
        installmentCount,
        installmentAmount
      });

      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const typeOptions = [
    { id: "Loan", label: t("loans.types.loan") },
    { id: "Advance", label: t("loans.types.advance") }
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
      title={t("loans.newLoanModalTitle")}
      description={t("loans.newLoanModalDesc")}
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
            form="new-loan-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("loans.createAction")}
          </Button>
        </>
      }
    >
      <form
        id="new-loan-form"
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
          label={t("loans.targetEmployee")}
          value={employmentId}
          onChange={setEmploymentId}
          legalEntityId={legalEntityId}
          isRequired
          placeholder={t("loans.selectEmployeePlaceholder")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("loans.type")}
            selectedKey={receivableType}
            onSelectionChange={(val) => {
              if (val) setReceivableType(String(val) as ReceivableType);
            }}
            options={typeOptions}
          />

          <TextField
            label={t("common.currency")}
            value={currency}
            onChange={setCurrency}
            isRequired
            isDisabled
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            type="number"
            label={t("loans.principalAmount")}
            value={String(principalAmount)}
            onChange={(val) => handlePrincipalChange(Number(val) || 0)}
            isRequired
          />

          <TextField
            type="number"
            label={t("loans.installmentCount")}
            value={String(installmentCount)}
            onChange={(val) => handleCountChange(Number(val) || 1)}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: "0.75rem" }}>
          <TextField
            type="number"
            label={t("loans.installmentAmount")}
            value={String(installmentAmount)}
            onChange={(val) => setInstallmentAmount(Number(val) || 0)}
            isRequired
            description={t("loans.installmentCalcNotice")}
          />
        </div>

        <TextField
          label={t("loans.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("loans.notesPlaceholder")}
        />
      </form>
    </Dialog>
  );
}
