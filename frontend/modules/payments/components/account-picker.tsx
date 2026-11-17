import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import type { PaymentAccount, PaymentAccountType } from "../types.ts";

export interface AccountPickerProps {
  readonly accounts: readonly PaymentAccount[];
  readonly selectedId?: string | undefined;
  readonly onSelect: (account: PaymentAccount | undefined) => void;
  readonly label: string;
  readonly filterType?: PaymentAccountType | undefined;
  readonly filterCurrency?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly disabled?: boolean | undefined;
}

export function AccountPicker({
  accounts,
  selectedId,
  onSelect,
  label,
  filterType,
  filterCurrency,
  isRequired,
  disabled
}: AccountPickerProps): React.JSX.Element {
  const { t } = useTranslation("payments");
  const [searchTerm, setSearchTerm] = useState("");

  const filtered = accounts.filter((acc) => {
    if (acc.status !== "Active") return false;
    if (filterType && acc.type !== filterType) return false;
    if (filterCurrency && acc.currencyCode && acc.currencyCode !== filterCurrency) return false;
    return true;
  });

  const selectedAccount = filtered.find((account) => account.id === selectedId);
  useEffect(() => {
    setSearchTerm(selectedAccount?.name ?? "");
  }, [selectedAccount?.id, selectedAccount?.name]);

  const options: ComboBoxOption[] = filtered.map((acc) => ({
    id: acc.id,
    label: acc.name,
    description: [
      acc.currencyCode || acc.currencyPolicy,
      acc.maskedIdentifier ? `[${acc.maskedIdentifier}]` : undefined
    ].filter(Boolean).join(" · ")
  }));

  return (
    <ComboBox
      label={label}
      selectedKey={selectedId || null}
      inputValue={searchTerm}
      onSelectionChange={(val) => {
        const found = accounts.find((a) => a.id === String(val ?? ""));
        setSearchTerm(found?.name ?? "");
        onSelect(found);
      }}
      onInputChange={setSearchTerm}
      options={options}
      placeholder={t("accounts.selectPlaceholder")}
      isRequired={isRequired}
      isDisabled={disabled}
      noResultsMessage={t("accounts.noResults")}
    />
  );
}
