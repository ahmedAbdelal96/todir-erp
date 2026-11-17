import React, { useState, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
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
  label,
  description,
  errorMessage,
  value,
  onChange,
  roleFilter,
  isRequired,
  isReadOnly,
  isLocked,
  lockedReason,
  placeholder,
  // Transactional forms must select mastered parties; creation belongs to Directory.
  enableQuickCreate = false
}: PartyPickerProps): React.JSX.Element {
  const { t } = useTranslation("directory");
  const partyLabel = label ?? t("partyPicker.label");
  const partyPlaceholder = placeholder ?? t("partyPicker.placeholder");
  const [inputValue, setInputValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
  const [quickCreateName, setQuickCreateName] = useState("");
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Fast responsive debounce: fires query after 120 ms
  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => {
      setDebouncedSearch(inputValue);
    }, 120);
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

  const roleLabel = (party: Party): string => (party.roles ?? [])
    .filter((role) => role.status === "Active")
    .map((role) => role.code === "Customer" ? t("partyPicker.customer") : t("partyPicker.supplier"))
    .join("، ");

  const businessContext = (party: Party): string => {
    const identifiers = party.identifiers ?? [];
    const addresses = party.addresses ?? [];
    const identifier = identifiers.find((item) => item.isPrimary)?.value ?? identifiers[0]?.value;
    const location = addresses.find((address) => address.isDefault)?.city ?? addresses[0]?.city;
    return [
      party.legalName && party.legalName !== party.displayName ? t("partyPicker.legalName", { name: party.legalName }) : undefined,
      roleLabel(party) || t("partyPicker.genericParty"),
      identifier ? t("partyPicker.officialNumber", { number: identifier }) : t("partyPicker.noOfficialNumber"),
      location ? t("partyPicker.location", { location }) : undefined
    ].filter(Boolean).join(" · ");
  };

  const options: ComboBoxOption[] = parties.map((p) => ({
    id: p.id,
    label: p.displayName,
    description: businessContext(p),
  }));

  const showQuickCreateButton = enableQuickCreate && !isReadOnly && !isLocked;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem", width: "100%" }}>
      <ComboBox
        label={partyLabel}
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
        isLoading={isLoading}
        onClear={() => onChange?.("", undefined)}
        placeholder={isLoading ? t("partyPicker.searching") : partyPlaceholder}
        noResultsMessage={t("partyPicker.noResults")}
        isRequired={isRequired}
        isReadOnly={isReadOnly}
        isLocked={isLocked}
        lockedReason={lockedReason}
        onCreateNew={showQuickCreateButton ? (term) => {
          setQuickCreateName(term);
          setIsQuickCreateOpen(true);
        } : undefined}
        createActionLabel={(term) => term ? t("partyPicker.createWithName", { name: term }) : t("partyPicker.createNewParty")}
      />

      {enableQuickCreate && (
        <QuickCreatePartyDialog
          isOpen={isQuickCreateOpen}
          onOpenChange={setIsQuickCreateOpen}
          defaultRole={roleFilter}
          initialDisplayName={quickCreateName || inputValue}
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
