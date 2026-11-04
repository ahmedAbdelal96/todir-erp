import React from "react";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { usePayrollPeriods } from "../hooks/use-payroll-queries.ts";

export interface PayrollPeriodPickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (periodId: string) => void;
  readonly legalEntityId: string;
  readonly groupId: string;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function PayrollPeriodPicker({
  label,
  value,
  onChange,
  legalEntityId,
  groupId,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: PayrollPeriodPickerProps): React.JSX.Element {
  const periodsQuery = usePayrollPeriods(legalEntityId, groupId, { pageSize: 100 });
  const periods = periodsQuery.data ?? [];

  const options: ComboBoxOption[] = periods.map((p) => ({
    id: p.id,
    label: `${p.periodStart} ↔ ${p.periodEnd} (${p.status})`
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(key ? String(key) : "")}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || !groupId || periodsQuery.isLoading}
      placeholder={placeholder}
      errorMessage={errorMessage}
    />
  );
}
