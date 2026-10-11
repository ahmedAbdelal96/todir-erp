import React, { useState, useEffect, useRef } from "react";
import type { Party, PartyRoleCode } from "../types.ts";
import { usePartyList } from "../hooks/party-queries.ts";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { QuickCreatePartyDialog } from "../features/quick-create-party-dialog.tsx";

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
  readonly enableQuickCreate?: boolean | undefined;
}

/**
 * PartyPicker — server-side debounced search with optional Quick Create.
 * Queries the real /parties API on every keystroke (debounced 350ms)
 * so large tenants are never pre-loaded in bulk.
 * Integrates Quick Create (Section 123 of canonical pack) when party does not exist.
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
  enableQuickCreate = true
}: PartyPickerProps): React.JSX.Element {
  const [inputValue, setInputValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
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

  const showQuickCreateButton = enableQuickCreate && !isReadOnly && !isLocked;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem", width: "100%" }}>
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

      {showQuickCreateButton && (
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button
            type="button"
            onClick={() => setIsQuickCreateOpen(true)}
            style={{
              border: 0,
              background: "none",
              color: "var(--color-brand-primary, #4f46e5)",
              fontSize: "0.75rem",
              fontWeight: 600,
              cursor: "pointer",
              padding: "0.125rem 0.25rem",
              textDecoration: "underline"
            }}
          >
            + إضافة طرف جديد سريع
          </button>
        </div>
      )}

      {enableQuickCreate && (
        <QuickCreatePartyDialog
          isOpen={isQuickCreateOpen}
          onOpenChange={setIsQuickCreateOpen}
          defaultRole={roleFilter}
          onCreated={(created) => {
            if (onChange) {
              onChange(created.id, created);
            }
          }}
        />
      )}
    </div>
  );
}
