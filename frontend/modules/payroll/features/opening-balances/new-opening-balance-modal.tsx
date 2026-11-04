import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrEmployeePicker } from "../../../hr/public.ts";
import { useCreateOpeningBalanceMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { OpeningBalanceType } from "../../types.ts";

export interface NewOpeningBalanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewOpeningBalanceModal({
  isOpen,
  onClose,
  legalEntityId
}: NewOpeningBalanceModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreateOpeningBalanceMutation(legalEntityId);

  const [employmentId, setEmploymentId] = useState("");
  const [balanceType, setBalanceType] = useState<OpeningBalanceType>("NetPay");
  const [asOfDate, setAsOfDate] = useState(new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<number>(0);
  const [currency, setCurrency] = useState("EGP");
  const [reference, setReference] = useState("");
  const [installmentAmount, setInstallmentAmount] = useState<number>(0);
  const [installmentCount, setInstallmentCount] = useState<number>(1);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setEmploymentId("");
    setBalanceType("NetPay");
    setAsOfDate(new Date().toISOString().slice(0, 10));
    setAmount(0);
    setCurrency("EGP");
    setReference("");
    setInstallmentAmount(0);
    setInstallmentCount(1);
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!employmentId) {
      setErrorMessage(t("openingBalances.employeeRequired"));
      return;
    }
    if (amount <= 0) {
      setErrorMessage(t("openingBalances.amountPositiveRequired"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        employmentId,
        balanceType,
        asOfDate,
        amount,
        currencyCode: currency,
        sourceReference: reference.trim() || "MIGRATION",
        installmentAmount: balanceType === "Loan" || balanceType === "Advance" ? installmentAmount : null,
        installmentCount: balanceType === "Loan" || balanceType === "Advance" ? installmentCount : null
      });

      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const balanceTypeOptions = [
    { id: "NetPay", label: t("openingBalances.types.netPay") },
    { id: "Tax", label: t("openingBalances.types.tax") },
    { id: "Insurance", label: t("openingBalances.types.insurance") },
    { id: "Loan", label: t("openingBalances.types.loan") },
    { id: "Advance", label: t("openingBalances.types.advance") }
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
      title={t("openingBalances.newModalTitle")}
      description={t("openingBalances.newModalDesc")}
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
            form="new-opening-balance-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("openingBalances.createAction")}
          </Button>
        </>
      }
    >
      <form
        id="new-opening-balance-form"
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
          label={t("openingBalances.targetEmployee")}
          value={employmentId}
          onChange={setEmploymentId}
          legalEntityId={legalEntityId}
          isRequired
          placeholder={t("openingBalances.selectEmployeePlaceholder")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("openingBalances.balanceType")}
            selectedKey={balanceType}
            onSelectionChange={(val) => {
              if (val) setBalanceType(String(val) as OpeningBalanceType);
            }}
            options={balanceTypeOptions}
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
            label={t("openingBalances.amount")}
            value={String(amount)}
            onChange={(val) => setAmount(Number(val) || 0)}
            isRequired
          />

          <LocalDateField
            label={t("openingBalances.asOfDate")}
            value={asOfDate}
            onChange={setAsOfDate}
            isRequired
          />
        </div>

        {(balanceType === "Loan" || balanceType === "Advance") && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <TextField
              type="number"
              label={t("loans.installmentAmount")}
              value={String(installmentAmount)}
              onChange={(val) => setInstallmentAmount(Number(val) || 0)}
              isRequired
            />
            <TextField
              type="number"
              label={t("loans.installmentCount")}
              value={String(installmentCount)}
              onChange={(val) => setInstallmentCount(Number(val) || 1)}
              isRequired
            />
          </div>
        )}

        <TextField
          label={t("openingBalances.reference")}
          value={reference}
          onChange={setReference}
          placeholder={t("openingBalances.referencePlaceholder")}
        />
      </form>
    </Dialog>
  );
}
