import React from "react";
import type { Party, PartyRoleCode } from "../types.ts";
import { usePartyList } from "../hooks/party-queries.ts";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";

export interface PartyPickerProps {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly value?: string | undefined;
  readonly onChange?: ((partyId: string, party?: Party) => void) | undefined;
  readonly roleFilter?: PartyRoleCode | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly placeholder?: string | undefined;
}

export function PartyPicker({
  label = "الطرف التجاري",
  description,
  errorMessage,
  value,
  onChange,
  roleFilter,
  isRequired,
  isReadOnly,
  isLocked,
  lockedReason,
  placeholder = "ابحث عن الطرف بالاسم أو الرمز أو الرقم الضريبي...",
}: PartyPickerProps): React.JSX.Element {
  const { data, isLoading } = usePartyList({
    role: roleFilter,
    pageSize: 50,
  });

  const parties: readonly Party[] = data?.items ?? [];

  const options: ComboBoxOption[] = parties.map((p) => ({
    id: p.id,
    label: p.displayName,
    description: p.legalName && p.legalName !== p.displayName ? p.legalName : undefined,
  }));

  return (
    <ComboBox
      label={label}
      description={description}
      errorMessage={errorMessage}
      selectedKey={value ?? null}
      onSelectionChange={(selected) => {
        const id = String(selected ?? "");
        const matched = parties.find((p) => p.id === id);
        if (onChange) {
          onChange(id, matched);
        }
      }}
      options={options}
      placeholder={isLoading ? "جارٍ التحميل..." : placeholder}
      isRequired={isRequired}
      isReadOnly={isReadOnly}
      isLocked={isLocked}
      lockedReason={lockedReason}
    />
  );
}
