import React from "react";
import { useTranslation } from "react-i18next";
import { Select } from "../../../components/primitives/select.tsx";
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

  const filtered = accounts.filter((acc) => {
    if (acc.status !== "Active") return false;
    if (filterType && acc.type !== filterType) return false;
    if (filterCurrency && acc.currencyCode && acc.currencyCode !== filterCurrency) return false;
    return true;
  });

  const options = filtered.map((acc) => ({
    id: acc.id,
    label: `${acc.name} (${acc.currencyCode || acc.currencyPolicy}) ${acc.maskedIdentifier ? `[${acc.maskedIdentifier}]` : ""}`
  }));

  return (
    <Select
      label={label}
      selectedKey={selectedId || null}
      onSelectionChange={(val) => {
        const found = accounts.find((a) => a.id === String(val ?? ""));
        onSelect(found);
      }}
      options={options}
      placeholder={t("accounts.selectPlaceholder")}
      isRequired={isRequired}
      isDisabled={disabled}
    />
  );
}
