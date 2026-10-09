import React, { useState } from "react";
import { ComboBox } from "../../../design-system/primitives/combobox.tsx";
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
  label = "الحساب المالي",
  selectedAccountCode,
  onSelectAccount,
  isDisabled = false,
  isRequired = false
}: AccountPickerProps): React.JSX.Element {
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
      label={label}
      placeholder={isLoading ? "جارٍ البحث..." : "ابحث برمز الحساب أو الاسم..."}
      options={options}
      selectedKey={selectedAccountCode ?? null}
      onSelectionChange={handleSelectionChange}
      isDisabled={isDisabled}
      isRequired={isRequired}
    />
  );
}
