import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { useCreatePayrollGroupMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollGroupFrequency, HolidayAdjustment } from "../../types.ts";

export interface NewPayrollGroupModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewPayrollGroupModal({
  isOpen,
  onClose,
  legalEntityId
}: NewPayrollGroupModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreatePayrollGroupMutation(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [frequency, setFrequency] = useState<PayrollGroupFrequency>("Monthly");
  const [payDay, setPayDay] = useState("28");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [holidayAdjustment, setHolidayAdjustment] = useState<HolidayAdjustment>("NextWorkingDay");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCode("");
    setName("");
    setFrequency("Monthly");
    setPayDay("28");
    setCurrencyCode("EGP");
    setHolidayAdjustment("NextWorkingDay");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) {
      setErrorMessage(t("setup.groupCode") + " / " + t("setup.groupNameAr"));
      return;
    }

    const payDayNum = parseInt(payDay, 10);
    if (isNaN(payDayNum) || payDayNum < 1 || payDayNum > 31) {
      setErrorMessage(t("setup.cutoffDaysBeforePayDate"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        code: code.trim(),
        name: name.trim(),
        frequency,
        payDay: payDayNum,
        currencyCode: currencyCode.trim().toUpperCase(),
        holidayAdjustment
      });

      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const frequencyOptions = [
    { id: "Monthly", label: t("setup.frequencies.monthly") },
    { id: "Weekly", label: t("setup.frequencies.weekly") },
    { id: "Biweekly", label: t("setup.frequencies.biWeekly") },
    { id: "SemiMonthly", label: t("setup.frequencies.semiMonthly") }
  ];

  const holidayOptions = [
    { id: "NextWorkingDay", label: t("setup.holidayPolicies.afterHoliday") },
    { id: "PreviousWorkingDay", label: t("setup.holidayPolicies.beforeHoliday") },
    { id: "None", label: t("setup.holidayPolicies.noAdjustment") }
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
      title={t("setup.newGroupModalTitle")}
      description={t("setup.newGroupModalDesc")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="new-payroll-group-form" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("actions.saving") : t("setup.saveGroupAction")}
          </Button>
        </>
      }
    >
      <form id="new-payroll-group-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.875rem" }}>
          <TextField
            label={t("setup.groupCode")}
            value={code}
            onChange={setCode}
            isRequired
          />

          <TextField
            label={t("setup.groupNameAr")}
            value={name}
            onChange={setName}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "0.875rem" }}>
          <Select
            label={t("setup.payFrequency")}
            selectedKey={frequency}
            onSelectionChange={(val) => {
              if (val) setFrequency(String(val) as PayrollGroupFrequency);
            }}
            options={frequencyOptions}
            isRequired
          />

          <TextField
            label={t("setup.cutoffDaysBeforePayDate")}
            value={payDay}
            onChange={setPayDay}
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

        <Select
          label={t("setup.holidayPolicy")}
          selectedKey={holidayAdjustment}
          onSelectionChange={(val) => {
            if (val) setHolidayAdjustment(String(val) as HolidayAdjustment);
          }}
          options={holidayOptions}
        />
      </form>
    </Dialog>
  );
}
