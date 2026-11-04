import React from "react";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { usePayrollGroups } from "../hooks/use-payroll-queries.ts";

export interface PayrollGroupPickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (groupId: string) => void;
  readonly legalEntityId: string;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function PayrollGroupPicker({
  label,
  value,
  onChange,
  legalEntityId,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: PayrollGroupPickerProps): React.JSX.Element {
  const groupsQuery = usePayrollGroups(legalEntityId, { pageSize: 100 });
  const groups = groupsQuery.data ?? [];

  const options: ComboBoxOption[] = groups.map((g) => ({
    id: g.id,
    label: `${g.name} (${g.code} — ${g.currencyCode})`
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(key ? String(key) : "")}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || groupsQuery.isLoading}
      placeholder={placeholder}
      errorMessage={errorMessage}
    />
  );
}
