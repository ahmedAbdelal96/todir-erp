import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PayrollGroupPicker } from "../../components/payroll-group-picker.tsx";
import { usePayrollGroups, useCreatePayrollPeriodMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollGroup } from "../../types.ts";

export interface NewPayrollPeriodModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultGroupId?: string | undefined;
}

const computePeriodDates = (group?: PayrollGroup) => {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const pad = (n: number) => n.toString().padStart(2, "0");
  const monthStr = pad(month + 1);
  const lastDay = new Date(year, month + 1, 0).getDate();

  const payDay = Math.min(Math.max(group?.payDay ?? 28, 1), lastDay);
  const cutoffDay = Math.max(1, payDay - 5);
  const calcDay = Math.min(lastDay, Math.max(cutoffDay + 1, payDay - 2));

  return {
    periodStart: `${year}-${monthStr}-01`,
    periodEnd: `${year}-${monthStr}-${pad(lastDay)}`,
    inputCutoff: `${year}-${monthStr}-${pad(cutoffDay)}`,
    calculationDate: `${year}-${monthStr}-${pad(calcDay)}`,
    plannedPayDate: `${year}-${monthStr}-${pad(payDay)}`
  };
};

export function NewPayrollPeriodModal({
  isOpen,
  onClose,
  legalEntityId,
  defaultGroupId
}: NewPayrollPeriodModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const groupsQuery = usePayrollGroups(legalEntityId);
  const groups = groupsQuery.data ?? [];

  const [groupId, setGroupId] = useState(defaultGroupId ?? "");
  const selectedGroup = groups.find((g) => g.id === (groupId || defaultGroupId));
  const defaults = computePeriodDates(selectedGroup);

  const [periodStart, setPeriodStart] = useState(defaults.periodStart);
  const [periodEnd, setPeriodEnd] = useState(defaults.periodEnd);
  const [inputCutoff, setInputCutoff] = useState(defaults.inputCutoff);
  const [calculationDate, setCalculationDate] = useState(defaults.calculationDate);
  const [plannedPayDate, setPlannedPayDate] = useState(defaults.plannedPayDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreatePayrollPeriodMutation(legalEntityId, groupId);

  const handleGroupChange = (newGroupId: string) => {
    setGroupId(newGroupId);
    const g = groups.find((item) => item.id === newGroupId);
    if (g) {
      const dates = computePeriodDates(g);
      setPeriodStart(dates.periodStart);
      setPeriodEnd(dates.periodEnd);
      setInputCutoff(dates.inputCutoff);
      setCalculationDate(dates.calculationDate);
      setPlannedPayDate(dates.plannedPayDate);
    }
  };

  const reset = () => {
    const freshGroup = groups.find((g) => g.id === defaultGroupId);
    const freshDefaults = computePeriodDates(freshGroup);
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
          onChange={handleGroupChange}
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
