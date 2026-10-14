import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ComboBox } from "../../../components/primitives/combobox.tsx";
import { useAccountSearch } from "../hooks/journal-queries.ts";
import type { Account } from "../types.ts";

export interface AccountPickerProps {
  readonly label?: string | undefined;
  readonly selectedAccountCode?: string | undefined;
  readonly onSelectAccount?: ((account: Account | null) => void) | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly isRequired?: boolean | undefined;
}

export function AccountPicker({
  label,
  selectedAccountCode,
  onSelectAccount,
  isDisabled = false,
  isRequired = false
}: AccountPickerProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [searchTerm, setSearchTerm] = useState("");
  const { data: accounts = [], isLoading } = useAccountSearch(searchTerm || selectedAccountCode || "");

  const options = accounts.map((acc) => ({
    id: acc.code,
    label: `${acc.code} — ${acc.name} (${acc.currency})`
  }));

  // Ensure selected item is always in the options list
  if (selectedAccountCode && !options.some((o) => o.id === selectedAccountCode)) {
    options.unshift({
      id: selectedAccountCode,
      label: selectedAccountCode
    });
  }

  const handleSelectionChange = (key: string | number | null) => {
    if (!key) {
      onSelectAccount?.(null);
      return;
    }
    const found = accounts.find((a) => a.code === String(key)) || null;
    onSelectAccount?.(found ?? { code: String(key), name: String(key), type: "Asset", currency: "SAR" });
  };

  return (
    <ComboBox
      label={label ?? t("accountPicker.label")}
      placeholder={isLoading ? t("accountPicker.searching") : t("accountPicker.placeholder")}
      options={options}
      selectedKey={selectedAccountCode ?? null}
      onSelectionChange={handleSelectionChange}
      isDisabled={isDisabled}
      isRequired={isRequired}
    />
  );
}
