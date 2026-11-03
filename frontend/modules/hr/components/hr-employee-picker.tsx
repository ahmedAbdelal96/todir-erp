import React from "react";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { useEmployees } from "../hooks/use-hr-queries.ts";

export interface HrEmployeePickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (employeeId: string) => void;
  readonly legalEntityId?: string | null | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function HrEmployeePicker({
  label,
  value,
  onChange,
  legalEntityId,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: HrEmployeePickerProps): React.JSX.Element {
  const employeesQuery = useEmployees({ legalEntityId: legalEntityId ?? undefined, pageSize: 100 });
  const employees = employeesQuery.data?.items ?? [];

  const options: ComboBoxOption[] = employees.map((emp) => ({
    id: emp.id,
    label: `${emp.displayName}${emp.employmentNumber ? ` (${emp.employmentNumber})` : ""}`
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(key ? String(key) : "")}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || employeesQuery.isLoading}
      placeholder={placeholder}
      errorMessage={errorMessage}
    />
  );
}
