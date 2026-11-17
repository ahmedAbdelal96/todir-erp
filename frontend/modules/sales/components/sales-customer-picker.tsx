import React from "react";
import { useTranslation } from "react-i18next";
import { PartyPicker } from "../../directory/public.ts";

export interface SalesCustomerPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (partyId: string) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

/** Sales transactions select a real Directory customer, never a local fixture. */
export function SalesCustomerPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: SalesCustomerPickerProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  return (
    <PartyPicker
      label={label ?? t("quotations.customer")}
      value={value ?? undefined}
      onChange={(partyId) => onChange(partyId)}
      roleFilter="Customer"
      isRequired={isRequired}
      isReadOnly={disabled}
      enableQuickCreate={false}
      placeholder={t("quotations.searchCustomerPlaceholder")}
    />
  );
}
