import React from "react";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { useDepartments } from "../hooks/use-hr-queries.ts";

export interface HrDepartmentPickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (departmentId: string) => void;
  readonly legalEntityId: string;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function HrDepartmentPicker({
  label,
  value,
  onChange,
  legalEntityId,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: HrDepartmentPickerProps): React.JSX.Element {
  const departmentsQuery = useDepartments(legalEntityId);
  const departments = departmentsQuery.data ?? [];

  const options: ComboBoxOption[] = departments.map((dept) => ({
    id: dept.id,
    label: `${dept.nameAr} (${dept.code})`
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(key ? String(key) : "")}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || departmentsQuery.isLoading}
      placeholder={placeholder}
      errorMessage={errorMessage}
    />
  );
}
