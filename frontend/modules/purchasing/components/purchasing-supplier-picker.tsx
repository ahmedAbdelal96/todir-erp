import React from "react";
import { useTranslation } from "react-i18next";
import { PartyPicker } from "../../directory/public.ts";

export interface PurchasingSupplierPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (supplierPartyId: string) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

/** Purchase transactions select a real Directory supplier, never a local fixture or typed identifier. */
export function PurchasingSupplierPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: PurchasingSupplierPickerProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  return (
    <PartyPicker
      label={label ?? t("orders.supplier")}
      value={value ?? undefined}
      onChange={(partyId) => onChange(partyId)}
      roleFilter="Supplier"
      isRequired={isRequired}
      isReadOnly={disabled}
      enableQuickCreate={false}
      placeholder={t("suppliers.searchPlaceholder")}
    />
  );
}
