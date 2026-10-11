import React, { useState, useEffect, useRef } from "react";
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

/**
 * PartyPicker — server-side debounced search.
 * Queries the real /parties API on every keystroke (debounced 350ms)
 * so large tenants are never pre-loaded in bulk.
 */
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
  const [inputValue, setInputValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounce input: only fire query after 350 ms of user inactivity
  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => {
      setDebouncedSearch(inputValue);
    }, 350);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [inputValue]);

  const { data, isLoading } = usePartyList({
    search: debouncedSearch || undefined,
    role: roleFilter,
    pageSize: 20,
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
      inputValue={inputValue}
      onInputChange={setInputValue}
      onSelectionChange={(selected) => {
        const id = String(selected ?? "");
        const matched = parties.find((p) => p.id === id);
        if (onChange) {
          onChange(id, matched);
        }
      }}
      options={options}
      placeholder={isLoading ? "جارٍ البحث..." : placeholder}
      isRequired={isRequired}
      isReadOnly={isReadOnly}
      isLocked={isLocked}
      lockedReason={lockedReason}
    />
  );
}
