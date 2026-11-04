import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PayrollGroupPicker } from "../../components/payroll-group-picker.tsx";
import { usePayrollGroups, useCreatePayrollPeriodMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollGroup, HolidayAdjustment } from "../../types.ts";

export interface NewPayrollPeriodModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultGroupId?: string | undefined;
}

function adjustForWeekend(date: Date, rule: HolidayAdjustment = "NextWorkingDay"): Date {
  if (rule === "None") return date;
  const d = new Date(date.getTime());
  while (d.getDay() === 0 || d.getDay() === 6) {
    d.setDate(d.getDate() + (rule === "PreviousWorkingDay" ? -1 : 1));
  }
  return d;
}

const formatDate = (d: Date): string => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const computePeriodDates = (group?: PayrollGroup) => {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const lastDay = new Date(year, month + 1, 0).getDate();

  const periodStartDate = new Date(year, month, 1);
  const periodEndDate = new Date(year, month, lastDay);

  const rawPayDay = group?.payDay ?? 28;
  const targetPayDay = Math.min(Math.max(rawPayDay, 1), 31);

  // If payday is early in the month (< 15), pay date applies to the 1st week of next month.
  // Otherwise, it falls within the current period month.
  const payMonth = targetPayDay < 15 ? month + 1 : month;
  const maxDayInPayMonth = new Date(year, payMonth + 1, 0).getDate();
  const plannedPayDateRaw = new Date(year, payMonth, Math.min(targetPayDay, maxDayInPayMonth));

  const plannedPayDateObj = adjustForWeekend(plannedPayDateRaw, group?.holidayAdjustment);
  // Cutoff is 4 days before pay date
  const cutoffObj = new Date(plannedPayDateObj.getTime() - 4 * 24 * 60 * 60 * 1000);
  // Calculation is 2 days before pay date
  const calcObj = new Date(plannedPayDateObj.getTime() - 2 * 24 * 60 * 60 * 1000);

  return {
    periodStart: formatDate(periodStartDate),
    periodEnd: formatDate(periodEndDate),
    inputCutoff: formatDate(cutoffObj),
    calculationDate: formatDate(calcObj),
    plannedPayDate: formatDate(plannedPayDateObj)
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

  useEffect(() => {
    if ((!groupId || !selectedGroup) && groups.length > 0) {
      const firstGroup = groups[0];
      const activeId = defaultGroupId && groups.some((g) => g.id === defaultGroupId)
        ? defaultGroupId
        : (firstGroup ? firstGroup.id : "");
      if (activeId) {
        setGroupId(activeId);
        const matched = groups.find((g) => g.id === activeId);
        if (matched) {
          const dates = computePeriodDates(matched);
          setPeriodStart(dates.periodStart);
          setPeriodEnd(dates.periodEnd);
          setInputCutoff(dates.inputCutoff);
          setCalculationDate(dates.calculationDate);
          setPlannedPayDate(dates.plannedPayDate);
        }
      }
    }
  }, [groups, defaultGroupId, groupId, selectedGroup]);

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
