import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PayrollGroupPicker } from "../../components/payroll-group-picker.tsx";
import { useCreatePayrollPeriodMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface NewPayrollPeriodModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultGroupId?: string | undefined;
}

const getDefaultPeriodDates = () => {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const pad = (n: number) => n.toString().padStart(2, "0");
  const monthStr = pad(month + 1);
  const lastDay = new Date(year, month + 1, 0).getDate();

  return {
    periodStart: `${year}-${monthStr}-01`,
    periodEnd: `${year}-${monthStr}-${pad(lastDay)}`,
    inputCutoff: `${year}-${monthStr}-23`,
    calculationDate: `${year}-${monthStr}-25`,
    plannedPayDate: `${year}-${monthStr}-28`
  };
};

export function NewPayrollPeriodModal({
  isOpen,
  onClose,
  legalEntityId,
  defaultGroupId
}: NewPayrollPeriodModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const defaults = getDefaultPeriodDates();

  const [groupId, setGroupId] = useState(defaultGroupId ?? "");
  const [periodStart, setPeriodStart] = useState(defaults.periodStart);
  const [periodEnd, setPeriodEnd] = useState(defaults.periodEnd);
  const [inputCutoff, setInputCutoff] = useState(defaults.inputCutoff);
  const [calculationDate, setCalculationDate] = useState(defaults.calculationDate);
  const [plannedPayDate, setPlannedPayDate] = useState(defaults.plannedPayDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreatePayrollPeriodMutation(legalEntityId, groupId);

  const reset = () => {
    const freshDefaults = getDefaultPeriodDates();
    setGroupId(defaultGroupId ?? "");
    setPeriodStart(freshDefaults.periodStart);
    setPeriodEnd(freshDefaults.periodEnd);
    setInputCutoff(freshDefaults.inputCutoff);
    setCalculationDate(freshDefaults.calculationDate);
    setPlannedPayDate(freshDefaults.plannedPayDate);
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!groupId) {
      setErrorMessage(t("setup.selectGroup"));
      return;
    }

    if (!periodStart || !periodEnd || !inputCutoff || !calculationDate || !plannedPayDate) {
      setErrorMessage(t("setup.periodStartDate") + " / " + t("setup.periodEndDate"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        payrollGroupId: groupId,
        periodStart,
        periodEnd,
        inputCutoff,
        calculationDate,
        plannedPayDate
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
      title={t("setup.newPeriodModalTitle")}
      description={t("setup.newPeriodModalDesc")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="new-payroll-period-form" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("actions.saving") : t("setup.savePeriodAction")}
          </Button>
        </>
      }
    >
      <form id="new-payroll-period-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <PayrollGroupPicker
          label={t("setup.selectGroup")}
          value={groupId}
          onChange={setGroupId}
          legalEntityId={legalEntityId}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.875rem" }}>
          <LocalDateField
            label={t("setup.periodStartDate")}
            value={periodStart}
            onChange={setPeriodStart}
            isRequired
          />

          <LocalDateField
            label={t("setup.periodEndDate")}
            value={periodEnd}
            onChange={setPeriodEnd}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "0.875rem" }}>
          <LocalDateField
            label={t("setup.periods.cutoffDate")}
            value={inputCutoff}
            onChange={setInputCutoff}
            isRequired
          />

          <LocalDateField
            label={t("setup.calculationDate")}
            value={calculationDate}
            onChange={setCalculationDate}
            isRequired
          />

          <LocalDateField
            label={t("setup.plannedPayDate")}
            value={plannedPayDate}
            onChange={setPlannedPayDate}
            isRequired
          />
        </div>
      </form>
    </Dialog>
  );
}
