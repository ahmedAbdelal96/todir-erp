import React from "react";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { usePositions } from "../hooks/use-hr-queries.ts";

export interface HrPositionPickerProps {
  readonly label: string;
  readonly value: string | null | undefined;
  readonly onChange: (positionId: string) => void;
  readonly legalEntityId: string;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly errorMessage?: string | undefined;
}

export function HrPositionPicker({
  label,
  value,
  onChange,
  legalEntityId,
  isRequired = false,
  isDisabled = false,
  placeholder,
  errorMessage
}: HrPositionPickerProps): React.JSX.Element {
  const positionsQuery = usePositions(legalEntityId);
  const positions = positionsQuery.data ?? [];

  const options: ComboBoxOption[] = positions.map((pos) => ({
    id: pos.id,
    label: `${pos.nameAr} (${pos.code})`
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(key ? String(key) : "")}
      options={options}
      isRequired={isRequired}
      isDisabled={isDisabled || positionsQuery.isLoading}
      placeholder={placeholder}
      errorMessage={errorMessage}
    />
  );
}
