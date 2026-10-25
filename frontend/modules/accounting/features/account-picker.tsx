import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ComboBox } from "../../../components/primitives/combobox.tsx";
import { useAccountSearch, useAccounts } from "../hooks/use-ledger-queries.ts";
import type { AccountDto } from "../types.ts";

export interface AccountPickerProps {
  readonly legalEntityId: string;
  readonly label?: string | undefined;
  readonly selectedAccountId?: string | undefined;
  readonly onSelectAccount?: ((account: AccountDto | null) => void) | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly isRequired?: boolean | undefined;
  readonly onlyDirectPosting?: boolean | undefined;
}

export function AccountPicker({
  legalEntityId,
  label,
  selectedAccountId,
  onSelectAccount,
  isDisabled = false,
  isRequired = false,
  onlyDirectPosting = true
}: AccountPickerProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [searchTerm, setSearchTerm] = useState("");

  const { data: searchResults, isLoading: isSearchLoading } = useAccountSearch(
    legalEntityId,
    searchTerm || undefined
  );
  const { data: allAccounts = [], isLoading: isAllLoading } = useAccounts(legalEntityId);

  const rawList = (searchResults?.items && searchResults.items.length > 0)
    ? searchResults.items
    : allAccounts;

  const filteredAccounts = onlyDirectPosting
    ? rawList.filter((a) => a.allowDirectPosting && a.status === "Active")
    : rawList;

  const options = filteredAccounts.map((acc) => ({
    id: acc.id,
    label: `${acc.code} — ${acc.nameAr} (${acc.nameEn})`
  }));

  // Ensure current selection is present in options
  if (selectedAccountId && !options.some((o) => o.id === selectedAccountId)) {
    const selectedAcc = allAccounts.find((a) => a.id === selectedAccountId);
    if (selectedAcc) {
      options.unshift({
        id: selectedAcc.id,
        label: `${selectedAcc.code} — ${selectedAcc.nameAr} (${selectedAcc.nameEn})`
      });
    }
  }

  const handleSelectionChange = (key: string | number | null) => {
    if (!key) {
      onSelectAccount?.(null);
      return;
    }
    const selected = allAccounts.find((a) => a.id === String(key)) || null;
    onSelectAccount?.(selected);
  };

  const isLoading = isSearchLoading || isAllLoading;

  return (
    <ComboBox
      label={label ?? t("accountPicker.label")}
      placeholder={isLoading ? t("accountPicker.searching") : t("accountPicker.placeholder")}
      options={options}
      selectedKey={selectedAccountId ?? null}
      onSelectionChange={handleSelectionChange}
      onInputChange={setSearchTerm}
      isDisabled={isDisabled}
      isRequired={isRequired}
    />
  );
}
