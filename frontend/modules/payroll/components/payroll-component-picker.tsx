import React from "react";
import { useTranslation } from "react-i18next";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { usePayrollComponents } from "../hooks/use-payroll-queries.ts";
import type { PayrollComponent } from "../types.ts";

export interface PayrollComponentPickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (componentId: string, component?: PayrollComponent) => void;
  readonly legalEntityId?: string | null | undefined;
  readonly categoryFilter?: "Earning" | "EmployeeDeduction" | "EmployerContribution" | "Recovery" | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function PayrollComponentPicker({
  label,
  value,
  onChange,
  legalEntityId,
  categoryFilter,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: PayrollComponentPickerProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const componentsQuery = usePayrollComponents(legalEntityId ?? "");
  const allComponents = componentsQuery.data ?? [];

  const filteredComponents = categoryFilter
    ? allComponents.filter((c) => c.category === categoryFilter)
    : allComponents;

  const options: ComboBoxOption[] = filteredComponents.map((c) => ({
    id: c.id,
    label: `${c.name} (${c.code})`
  }));

  const handleSelectionChange = (key: string | number | null) => {
    if (!key) {
      onChange("");
      return;
    }
    const idStr = String(key);
    const selected = allComponents.find((c) => c.id === idStr);
    onChange(idStr, selected);
  };

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={handleSelectionChange}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || componentsQuery.isLoading}
      placeholder={placeholder ?? t("inputs.selectComponentPlaceholder", "اختر بند الراتب...")}
      errorMessage={errorMessage}
    />
  );
}
